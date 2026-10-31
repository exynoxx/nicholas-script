/// Tuples: `(a, b)` construction, `t@k` / `t._1` member access (k a literal), and elementwise arithmetic
/// `(a,b) + (c,d)`. Their layout (a value struct of scalars, or a boxed object) is the kernel's (Repr).
module Features.Tuples

open Core
open Typer

let private index (x: E) (i: E) =
    match resolve x.t with
    | TTup ts ->
        match i.k with
        | Int k when abs (int k) < ts.Length + (if k < 0L then 1 else 0) ->
            let k = if k < 0L then int k + ts.Length else int k
            Some(typed (Call("tget", [ x; typed (Int(int64 k)) TInt ])) ts.[k])
        | Int k -> err $"tuple index {k} is out of range: the tuple has {ts.Length} values (0..{ts.Length - 1})"
        | _ -> err $"tuple index must be a literal 0..{ts.Length - 1}"
    | _ -> None

let private bins op (_: bool) (a: E) (b: E) =
    match resolve a.t with
    | TTup ts when List.contains op [ "+"; "-"; "*"; "/"; "%"; "^^" ] ->
        // elementwise: `(a,b) + (c,d)` is `(a+c, b+d)`; a non-tuple right operand applies to every element
        let ta, tb = Desugar.fresh "t", Desugar.fresh "t"
        let bTup = match resolve b.t with TTup us -> us.Length = ts.Length || err $"tuple {op}: arities {ts.Length} and {us.Length} differ" | _ -> false
        let at x i = mk (Index(mk (Var x), mk (Int(int64 i))))
        let elems = ts |> List.mapi (fun i _ -> mk (Bin(op, at ta i, (if bTup then at tb i else mk (Var tb)))))
        Some(typeE (mk (Seq([ Assign(ta, a); Assign(tb, b) ], mk (Call("tuple", elems))))))
    | _ -> None

let feature =
    { Feature.empty with
        name = "tuples"
        index = [ index ]
        bin = [ bins ]
        prims =
          [ "tuple", fun xs ->
                if xs.Length > 9 then err $"a tuple holds at most 9 values, not {xs.Length}; use an array"
                Some(typed (Call("tuple", List.map (value >> own) xs)) (TTup(xs |> List.map (fun x -> x.t)))) ]
        freshness = [ "tget", fun _ -> false ] // a member is shared with the tuple
        effects = [ "tuple", Feature.Total; "tget", Feature.Total ]
        llvm =
          [ "tuple", fun g e args vs ->
                if Repr.isValueTuple e.t then
                    // value tuple: an LLVM struct built in registers (bool members widened to i64)
                    args |> List.indexed |> List.fold (fun acc (i, a) ->
                        let m = if resolve a.t = TBool then g.instr $"zext i1 {vs.[i]} to i64" else vs.[i]
                        let mt = if resolve a.t = TFloat then "double" else "i64"
                        g.instr $"insertvalue {Repr.llty e.t} {acc}, {mt} {m}, {i}") "undef"
                else
                    let t = g.rt "ptr" "ns_tup_new" [ g.desc e.t ]
                    args |> List.iteri (fun i a -> g.rt "void" "ns_tup_set" [ "ptr", t; "i64", string i; "ptr", g.spill vs.[i] a.t ] |> ignore)
                    t
            "tget", fun g e args vs ->
                let x = args.Head
                let k = match args.[1].k with Int k -> k | _ -> failwith "tget: index must be a literal"
                if Repr.isValueTuple x.t then
                    let m = g.instr $"extractvalue {Repr.llty x.t} {vs.[0]}, {k}"
                    if resolve e.t = TBool then g.instr $"icmp ne i64 {m}, 0" else m
                else
                    // boxed tuple: the member sits at a fixed offset in the object; borrowed from it, so retain
                    let p = g.instr $"getelementptr i8, ptr {vs.[0]}, i64 {Repr.tupOffset x.t (int k)}"
                    // members are packed, so one after a bool member may be unaligned
                    let v = g.instr $"load {Repr.storage e.t}, ptr {p}, align 1"
                    let v = if resolve e.t = TBool then g.instr $"trunc i8 {v} to i1" else v
                    g.retain v e.t
                    v ] }
