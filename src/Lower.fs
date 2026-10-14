/// Type-directed rewrites into plain core. The typer calls these once it knows an operand's type,
/// then types the produced (untyped) core like any other code. Typed sub-expressions pass through unchanged.
module Lower

open Ast
open Core

let private v n = mk (Var n)
let private i64 n = mk (Int n)
let private call f args = mk (Call(f, args))
let private bin op a b = mk (Bin(op, a, b))
let private seq ss e = mk (Seq(ss, e))

let private splitBody (body: S list) =
    match List.rev body with
    | Do e :: rest -> List.rev rest, e
    | _ -> body, i64 0L

/// A chain of map/filter stages ending in map/filter/fold/scan/each (innermost first, the head's `src` is
/// the source) becomes ONE index loop: no intermediate arrays. A single stage is the plain case.
let iter (stages: Iter list) (srcTy: Ty) : E =
    let s, t, n, c, r, key = Desugar.fresh "s", Desugar.fresh "t", Desugar.fresh "n", Desugar.fresh "c", Desugar.fresh "r", Desugar.fresh "k"
    let first = Desugar.fresh "first"
    let it0, term = List.head stages, List.last stages
    let fused = stages.Length > 1
    // a graph iterates like the map node -> neighbours; a filter keeps the induced subgraph
    let graph = match srcTy with TGraph _ -> true | _ -> false
    let srcTy = if graph then TMap(TUnk, TUnk) else srcTy
    let isMap = (match srcTy with TMap _ -> true | _ -> false) && not fused
    let folding = term.kind = IFold || term.kind = IScan
    // result kind: a single stage keeps the source's kind; a fused chain yields an array
    // (or a string when only filters run over a string)
    let resTy =
        if not fused then srcTy
        elif srcTy = TStr && stages |> List.forall (fun st -> st.kind = IFilter) then TStr
        else TArr TUnk
    let snapshot =
        match srcTy with
        | TArr _ -> v s
        | TStr -> call "chars" [ v s ]
        | TMap _ when folding && not graph -> call "vals" [ v s ]
        | _ -> call "arr" [ v s ]
    // a range source is iterated as a counter (lo, lo+step, ...) instead of a materialised array
    let range = match it0.src.k with Call("range", [ a; b ]) -> Some(a, b) | _ -> None
    let pre =
        match range with
        | Some(a, b) ->
            let hi = Desugar.fresh "hi"
            [ Assign(s, a); Assign(hi, b)
              Assign(t, mk (If(bin "<=" (v s) (v hi), i64 1L, i64 -1L)))
              Assign(n, bin "*" (bin "-" (v hi) (v s)) (v t)) ]
        | None -> [ Assign(s, it0.src); Assign(t, snapshot); Assign(n, call "len" [ v t ]) ]
    let load =
        match range with
        | _ when isMap && not folding ->
            [ Assign(key, mk (Index(v t, v c))); Assign(it0.ix, v key); Assign(it0.el, mk (Index(v s, v key))) ]
        | Some _ -> [ Assign(it0.el, bin "+" (v s) (bin "*" (v c) (v t))); Assign(it0.ix, v c) ]
        | None -> [ Assign(it0.el, mk (Index(v t, v c))); Assign(it0.ix, v c) ]
    let bodyE (st: Iter) = let ss, value = splitBody st.body in seq ss value
    let skip = i64 0L
    let when' cond (then': S list) = Do(mk (If(cond, seq then' skip, skip)))
    let empty () =
        match resTy with
        | TMap _ -> call "mapof" [ mk (Arr []); mk (Arr []) ]
        | TSet _ -> call "set" [ mk (Arr []) ]
        | TQ _ -> call "Q" [ mk (Arr []) ]
        | TPQ _ -> call "PQ" [ mk (Arr []) ]
        | _ -> mk (Arr [])
    let add (x: E) =
        match resTy with
        | TMap _ -> SetIdx(v r, v key, x)
        | TSet _ | TQ _ | TPQ _ -> Do(bin "+" (v r) x)
        | _ -> Do(call "push" [ v r; x ])
    let terminal =
        match term.kind with
        | IMap -> [ add (bodyE term) ]
        | IFilter -> [ when' (bodyE term) [ add (v term.el) ] ]
        | IFold | IScan ->
            let push = if term.kind = IScan then [ Do(call "push" [ v r; v term.acc ]) ] else []
            [ Do(mk (If(v first, seq [ Assign(term.acc, v term.el); Assign(first, mk (Bool false)) ] skip,
                        seq [ Assign(term.acc, bodyE term) ] skip))) ] @ push
        | IEach -> [ Do(bodyE term) ]
    // stage i has its el/ix assigned; pass the element on to stage i+1 (with its own position counter)
    let counters = stages |> List.map (fun _ -> Desugar.fresh "k")
    let rec pipe i : S list =
        if i = stages.Length - 1 then terminal
        else
            let st, nxt, k = stages.[i], stages.[i + 1], counters.[i + 1]
            let pass (x: E) = [ Assign(nxt.el, x); Assign(nxt.ix, v k); Assign(k, bin "+" (v k) (i64 1L)) ] @ pipe (i + 1)
            match st.kind with
            | IMap -> pass (bodyE st)
            | _ -> [ when' (bodyE st) (pass (v st.el)) ]
    let init =
        (counters |> List.tail |> List.map (fun k -> Assign(k, skip)))
        @ (if folding then [ Assign(first, mk (Bool true)) ] else [])
        @ (if term.kind <> IFold && term.kind <> IEach then [ Assign(r, empty ()) ] else [])
    let loop = [ Assign(c, skip); While(bin "<" (v c) (v n), load @ pipe 0 @ [ Assign(c, bin "+" (v c) (i64 1L)) ]) ]
    let emptyFold = [ when' (v first) [ Do(call "panic" [ mk (Str "fold of empty collection") ]) ] ]
    match term.kind with
    | IMap -> seq (pre @ init @ loop) (v r)
    | IFilter when graph -> seq (pre @ init @ loop) (call "subgraph" [ v s; call "keys" [ v r ] ])
    | IFilter -> seq (pre @ init @ loop) (if resTy = TStr then call "join" [ v r; mk (Str "") ] else v r)
    | IFold -> seq (pre @ init @ loop @ emptyFold) (v term.acc)
    | IScan -> seq (pre @ init @ loop @ emptyFold) (v r)
    | IEach -> seq (pre @ init @ loop) skip

/// `a op b` with an array on the left: elementwise, right side either scalar or array.
let broadcast op (a: E) (b: E) (bIsArr: bool) =
    let y = Desugar.fresh "y"
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    let rhs = if bIsArr then mk (Index(v y, v ix)) else v y
    seq [ Assign(y, b) ] (mk (Iter { kind = IMap; src = a; el = el; ix = ix; acc = acc; body = [ Do(bin op (v el) rhs) ] }))

/// Array element conversion `[a] -> [b]` for a scalar target type.
let convArr (a: E) (castName: string) =
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    mk (Iter { kind = IMap; src = a; el = el; ix = ix; acc = acc; body = [ Do(call castName [ v el ]) ] })
