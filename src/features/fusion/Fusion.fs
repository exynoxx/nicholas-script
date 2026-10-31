/// Iteration (internal): typing of every map/filter/fold/scan/each, fused into one loop per chain by the engine in
/// Lower.fs; the endings that stop a chain early (`.take`, `chain@k`, `.one`, `#` of a filter, `.set`); and the
/// core pass that moves a chain stored in a variable into its single consumer, so that consumer decides whether
/// an array is ever built.
module Features.Fusion

open System.Collections.Generic
open Ast
open Core
open Desugar
open Typer

/// Sources that stream into a chain instead of building an array (a `lazy` call): untyped source, whether the
/// chain's ending stops early -> Some(the source typed as a plain call, a feed wrapping a sink block when it streams).
let streamSources = ResizeArray<E -> bool -> (E * (E -> E) option) option>()

/// A chain of map/filter stages feeding `it` becomes one loop; `limit` (from `.take:k`) stops it after k results.
/// `count` (`#` of a filter): over an array, the loop counts the kept elements instead of collecting them.
/// `at` (`chain@k`, k a literal >= 0 or a variable): index the result; a fused chain gets the limit k+1 (k when negative).
let rec typeIter (it: Iter) (limit: E option) (count: bool) (at: E option) : E = typeIterInto false it limit count at

/// toSet: the chain's consumer is `.set`; an array source adds straight into the set (no array in between).
and typeIterInto (toSet: bool) (it: Iter) (limit: E option) (count: bool) (at: E option) : E =
    // collect a chain of map/filter stages feeding this one (innermost first)
    let rec chain (it: Iter) acc =
        match it.src.k with
        | Iter inner when it.src.t = TUnk && (inner.kind = IMap || inner.kind = IFilter) -> chain inner (it :: acc)
        | _ -> it :: acc
    let stages = chain it []
    // a source that streams (a `lazy` call) into a chain whose ending stops early: the stages run per element, no array
    let stops = limit.IsSome || at.IsSome || (Lower.boolFold it).IsSome
    let lazySrc = streamSources |> Seq.tryPick (fun f -> f stages.Head.src stops)
    let feed = lazySrc |> Option.bind snd
    let src = match lazySrc with Some(plain, _) -> plain | None -> typeE stages.Head.src
    match resolve src.t, expect with
    | THole _, Some t -> unify src.t t |> ignore // a recursive call: its result type is the base case's
    | _ -> ()
    // an int in front of a block is the range 0..n: `5{_*_}`, `10[>6]`, `5/+`
    let src = match resolve src.t with TInt -> builtin "range" [ typed (Int 0L) TInt; src ] | _ -> src
    let limit =
        match at, resolve src.t with
        | Some i, (TArr _ | TStr) ->
            let plus1 = mk (Bin("+", i, mk (Int 1L)))
            Some(match i.k with Int _ -> plus1 | _ -> mk (If(mk (Bin("<", i, mk (Int 0L))), i, plus1)))
        | Some i, TCon(n, _) when Lower.drains.ContainsKey n ->
            let plus1 = mk (Bin("+", i, mk (Int 1L)))
            Some(match i.k with Int _ -> plus1 | _ -> mk (If(mk (Bin("<", i, mk (Int 0L))), i, plus1)))
        | _ -> limit
    let limit = limit |> Option.map (fun k -> conv (typeE k) TInt)
    let r =
      match resolve src.t with
      | TFloat | TBool | TTup _ | TFn _ as t ->
          err ($"cannot {showKind stages.Head.kind} over {showVal src}: it needs an int, array, string, map, set, queue or tree")
      | TArr _ when feed.IsSome -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false feed)
      | TArr _ when count -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) None true None)
      | TArr _ when toSet -> typeE (Lower.iterInto true ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      | TArr _ | TStr when stages.Length > 1 || limit.IsSome ->
          typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      // a set chain consumed by a fold/scan is lazy: `s{f}/+` sums every mapped value, it builds no (deduplicating) set
      // a queue drains (pop until empty) into an array, so its chains fuse like an array's
      | TCon(n, _) when Lower.drains.ContainsKey n -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      | TCon(n, _) when Lower.collects.Contains n && stages.Length > 1 && (it.kind = IFold || it.kind = IScan) ->
          typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      | _ ->
          // not fusable (maps/sets/queues keep their kind per stage): lower stage by stage
          let r = stages |> List.fold (fun (s: E) st -> typeE (Lower.iter [ { st with src = s } ] (resolve s.t) None false None)) src
          let r = match limit with Some k -> builtin "take" [ r; k ] | None -> r
          if count then builtin "len" [ r ] else r
    let r = if toSet && (feed.IsSome || match resolve src.t with TArr _ -> false | _ -> true) then builtin "set" [ r ] else r
    match at with Some i -> typeIndex r (typeE i) | None -> r

let private isChain (it: Iter) = it.kind = IMap || it.kind = IFilter

let private intercept (e: E) =
    match e.k with
    | Iter it -> Some(typeIter it None false None)
    | Call("take", [ { k = Iter it }; k ]) when isChain it && (lookup "take").IsNone -> Some(typeIter it (Some k) false None)
    // `chain@k` stops after k+1 results, like `(chain.take:k+1)@k`; `.one` is `@0`
    | Index({ k = Iter it }, ({ k = (Int _ | Var _) } as i)) when isChain it && (lookup "take").IsNone
                                                                 && (match i.k with Int n -> n >= 0L | _ -> true) ->
        Some(typeIter it None false (Some i))
    | Call("one", [ x ]) when (lookup "one").IsNone ->
        match x.k with
        | Iter _ -> Some(typeE { e with k = Index(x, mk (Int 0L)) })
        | _ ->
            let x = typeE x
            // ponytail: a set's `.one` copies it to an array; fine for the usual singleton set
            let x = match resolve x.t with TCon(n, _) when not (Lower.drains.ContainsKey n) -> stdCall "arr" [ x ] | _ -> x
            Some(typeIndex x (typeE (mk (Int 0L))))
    | Call("take1", [ x ]) when (lookup "take1").IsNone -> Some(typeE { e with k = Call("take", [ x; mk (Int 1L) ]) })
    | Call("len", [ { k = Iter it } ]) when isChain it && (lookup "len").IsNone -> Some(typeIter it None true None)
    | Call("set", [ { k = Iter it } ]) when isChain it && (lookup "set").IsNone -> Some(typeIterInto true it None false None)
    | _ -> None

let private impure = set [ "print"; "println"; "read"; "lines"; "write"; "spawn"; "pop"; "btadd"; "btdel"; "keep"; "memo"; "ret" ]

/// `a = chain; …; s` where `a` (an iteration: `0..n[…]`, `xs{…}`) is assigned once and used once, in a later
/// statement s of the same list, outside any loop or block of s: the chain moves into s, so its consumer decides whether an array
/// is ever built (`#a`, `a{…}.set`, `a/+` fuse into one loop). Applied left to right, `a = …; b = a{…}; c = #b` all fuse.
/// Only for a side-effect-free chain whose inputs neither s nor the statements before it change.
let fuseChains (prog: S list) : S list =
    let assigns, uses = Dictionary<string, int>(), Dictionary<string, int>()
    let bump (d: Dictionary<string, int>) n = d.[n] <- (match d.TryGetValue n with | true, c -> c + 1 | _ -> 1)
    let inFns = HashSet<string>() // assigned inside some block: a user call may change them
    let target s = match s with Assign(n, _) | SetIdx({ k = Var n }, _, _) -> Some n | _ -> None
    visitSS
        (fun e ->
            match e.k with
            | Var n -> bump uses n
            | Fn(_, body) -> visitSS ignore (fun s -> target s |> Option.iter (inFns.Add >> ignore)) body
            | _ -> ())
        (function Assign(n, _) -> bump assigns n | _ -> ()) prog
    let user (f: string) = "'?!".Contains f.[0] && assigns.ContainsKey(f.Substring 1)
    let pureCall (f: string) =
        if "'?!".Contains f.[0] then
            let g = f.Substring 1
            Parser.builtins.Contains g && not (impure.Contains g) && not (assigns.ContainsKey g)
        else f <> "debug"
    let local (n: string) = n.Contains '%' || n.Contains '\''
    let pureE (e: E) =
        let mutable ok = true
        visitE (fun x -> match x.k with Call(f, _) when not (pureCall f) -> ok <- false | _ -> ())
               (fun s -> match target s with Some n when not (local n) -> ok <- false | _ -> ()) e |> ignore
        ok
    let freeVars (e: E) =
        let vs = HashSet<string>()
        visitE (fun x -> match x.k with Var n -> vs.Add n |> ignore | _ -> ()) ignore e |> ignore
        vs
    // s may run before its use of the chain: it must not change what the chain reads
    let safe (s: S) (fv: HashSet<string>) =
        let mutable ok = true
        let calls = fv.Overlaps inFns
        visitSS (fun x ->
                    match x.k with
                    | Call(f, _) when user f && calls -> ok <- false
                    | Call(f, args) when not (pureCall f) && args |> List.exists (fun a -> fv.Overlaps(freeVars a)) -> ok <- false
                    | _ -> ())
                (fun s -> match target s with Some n when fv.Contains n -> ok <- false | _ -> ()) [ s ]
        ok
    // replaces the one `a` in s if it is evaluated once (not in a loop body, a block or match arms) and not
    // written through (`a@0 = 5` must not write into the chain's source)
    let subst (a: string) (chain: E) (s: S) =
        let mutable did = false
        let rec e (x: E) =
            match x.k with
            | Var n when n = a -> did <- true; chain
            | Iter it -> rebuild x (Iter { it with src = e it.src })
            | Call(f, sub :: rest) when perElement.Contains f -> rebuild x (Call(f, e sub :: rest))
            | Fn _ -> x
            | _ -> mapE e ss x
        and st s =
            match s with
            | While _ -> s
            | SetIdx(x, i, v) -> SetIdx(x, e i, e v)
            | _ -> mapS e ss s
        and ss b = List.map st b
        let s = st s
        if did then Some s else None
    let rec fuseSS (ss: S list) : S list =
        let arr = ResizeArray(ss |> List.map (mapS fuseE fuseSS))
        let mutable i = 0
        while i < arr.Count do
            match arr.[i] with
            | Assign(a, ({ k = Iter _ } as chain)) when assigns.[a] = 1 && uses.GetValueOrDefault a = 1 && pureE chain ->
                // the consumer: the later statement using `a`; it and every statement before it must be safe
                let mentions s = (freeVars (mk (Seq([ s ], mk (Int 0L))))).Contains a
                let fv = freeVars chain
                match seq { i + 1 .. arr.Count - 1 } |> Seq.tryFind (fun j -> mentions arr.[j]) with
                | Some j when seq { i + 1 .. j } |> Seq.forall (fun k -> safe arr.[k] fv) ->
                    match subst a chain arr.[j] with
                    | Some s -> arr.[j] <- s; arr.RemoveAt i
                    | None -> i <- i + 1
                | _ -> i <- i + 1
            | _ -> i <- i + 1
        List.ofSeq arr
    and fuseE (e: E) = mapE fuseE fuseSS e
    fuseSS prog

let feature =
    { Feature.empty with
        name = "fusion"
        builtins = [ "one"; "take1" ]
        intercept = [ intercept ]
        corePasses = [ 20, "fuse chains", fuseChains ] }
