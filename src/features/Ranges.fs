/// Ranges and slices: `lo..hi` (an int array, or a counter loop when only iterated), `x@a..b` (a slice of an array
/// or string). The `..` syntax is the kernel parser's (it is woven into index and range-end operands).
module Features.Ranges

open System.Collections.Generic
open Core
open Desugar
open Typer

/// A range end that is an array means its length: `0..xs` = `0..#xs`
let private bound (x: E) = match resolve x.t with TArr _ -> typed (Call("len", [ x ])) TInt | _ -> conv x TInt

/// `a = lo..hi` never builds the array when `a` is assigned once and only iterated (`a{…}`, `#a[…]`, `a/+`) or counted (`#a`):
/// the bounds go to temps and each `a` source becomes the range again, which Lower loops over as a counter.
let inlineRanges (prog: S list) : S list =
    let assigns = Dictionary<string, int>()
    let used = HashSet<string>() // `a` seen anywhere but as an iteration source
    let ranges = HashSet<string>()
    let rec scanE (e: E) =
        match e.k with
        | Var n -> used.Add n |> ignore; e
        | Iter({ src = { k = Var _ } } as it) -> mapE scanE scanSS { e with k = Iter { it with src = mk (Int 0L) } } |> ignore; e
        | Call("len", [ { k = Var _ } ]) -> e
        | _ -> mapE scanE scanSS e
    and scanSS ss =
        for s in ss do
            match s with
            | Assign(n, e) ->
                assigns.[n] <- (match assigns.TryGetValue n with | true, c -> c + 1 | _ -> 1)
                match e.k with Call("range", [ _; _ ]) -> ranges.Add n |> ignore | _ -> ()
            | _ -> ()
            mapS scanE scanSS s |> ignore
        ss
    scanSS prog |> ignore
    let bounds = Dictionary<string, string * string>()
    for n in ranges do
        if assigns.[n] = 1 && not (used.Contains n) then bounds.[n] <- (Desugar.fresh (n + "lo"), Desugar.fresh (n + "hi"))
    if bounds.Count = 0 then prog
    else
    let rec rwE (e: E) =
        match e.k with
        | Iter({ src = { k = Var n } } as it) when bounds.ContainsKey n ->
            let lo, hi = bounds.[n]
            let src = mk (Call("range", [ mk (Var lo); mk (Var hi) ]))
            mapE rwE rwSS (rebuild e (Iter { it with src = src }))
        | Call("len", [ { k = Var n } ]) when bounds.ContainsKey n ->
            let lo, hi = bounds.[n]
            rebuild e (Call("len", [ mk (Call("range", [ mk (Var lo); mk (Var hi) ])) ]))
        | _ -> mapE rwE rwSS e
    and rwSS ss =
        ss |> List.collect (fun s ->
            match s with
            | Assign(n, { k = Call("range", [ a; b ]) }) when bounds.ContainsKey n ->
                let lo, hi = bounds.[n]
                [ Assign(lo, rwE a); Assign(hi, rwE b) ]
            | _ -> [ mapS rwE rwSS s ])
    rwSS prog

let feature =
    { Feature.empty with
        name = "ranges"
        corePasses = [ 10, "inline ranges", inlineRanges ]
        prims =
          [ "range", (function [ a; b ] -> Some(typed (Call("range", [ bound a; bound b ])) (TArr TInt)) | _ -> None)
            "slice", (function
                | [ x; a; b ] ->
                    match resolve x.t with
                    | TArr _ | TStr -> Some(typed (Call("slice", [ x; bound a; bound b ])) x.t)
                    | _ -> err $"cannot slice {showVal x}"
                | _ -> None)
            // no array: `#(lo..hi)`
            "len", (function [ { k = Call("range", [ a; b ]) } ] -> Some(stdCall "abs" [ typed (Bin("-", b, a)) TInt ]) | _ -> None) ]
        // a slice of nested arrays shares the inner arrays
        freshness = [ "slice", fun e -> (match resolve e.t with TArr el -> (match resolve el with TArr _ -> false | _ -> true) | _ -> true) ]
        effects = [ "range", Feature.Total; "slice", Feature.Total ]
        llvm =
          [ "range", fun g _ _ vs -> g.rt "ptr" "ns_range" [ "i64", vs.[0]; "i64", vs.[1] ]
            "slice", fun g _ args vs ->
                g.rt "ptr" (if resolve args.Head.t = TStr then "ns_slice_s" else "ns_arr_slice") [ "ptr", vs.[0]; "i64", vs.[1]; "i64", vs.[2] ] ] }
