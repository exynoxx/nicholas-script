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
/// `count` (`#xs[p]`): a final filter only counts what it keeps.
let iter (stages: Iter list) (srcTy: Ty) (limit: E option) (count: bool) : E =
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
    // mapping a map, a top-level `_ = e` assigns the value and writes it back: `m{_ = _ + 1}` updates m in place
    let writeBack (st: Iter) =
        if not (isMap && (term.kind = IMap || term.kind = IEach)) then st.body
        else
            let last = st.body.Length - 1
            st.body |> List.indexed |> List.collect (fun (i, stmt) ->
                match stmt with
                | Do { k = Bin("=", { k = Var x }, e) } when x = st.el ->
                    [ Assign(x, e); SetIdx(v s, v key, v x) ] @ (if i = last then [ Do(v x) ] else [])
                | stmt -> [ stmt ])
    let bodyE (st: Iter) =
        let ss, value = splitBody (writeBack st)
        if st.post.IsEmpty then seq ss value
        else let x = Desugar.fresh "v" in seq (ss @ [ Assign(x, value) ] @ st.post) (v x)
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
        | IFilter when count -> [ when' (bodyE term) [ Assign(r, bin "+" (v r) (i64 1L)) ] ]
        | IFilter -> [ when' (bodyE term) [ add (v term.el) ] ]
        | IFold | IScan ->
            let push = if term.kind = IScan then [ Do(call "push" [ v r; v term.acc ]) ] else []
            // both branches give the accumulator, so a still-unknown `$` (a recursive call's result) keeps the
            // enclosing ternary's type hint instead of taking the then-branch's int
            [ Do(mk (If(v first, seq ([ Assign(term.acc, v term.el); Assign(first, mk (Bool false)) ] @ term.post) (v term.acc),
                        seq [ Assign(term.acc, bodyE term) ] (v term.acc)))) ] @ push
        | IEach ->
            // the body's value is dropped: a map that yields it builds no result array either
            match List.rev term.body with
            | Do({ k = Iter it } as e) :: rest when it.kind = IMap ->
                [ Do(bodyE { term with body = List.rev (Do { e with k = Iter { it with kind = IEach } } :: rest) }) ]
            | _ -> [ Do(bodyE term) ]
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
        @ (if count then [ Assign(r, skip) ] elif term.kind <> IFold && term.kind <> IEach then [ Assign(r, empty ()) ] else [])
    // `.take:k` on a map/filter chain: stop once k results are in (a negative k keeps the last -k, so it runs to the end)
    let lim = Desugar.fresh "lim"
    let pre = match limit with Some l -> Assign(lim, l) :: pre | None -> pre
    let more =
        match limit with
        | Some _ -> bin "&" (bin "<" (v c) (v n)) (bin "|" (bin "<" (v lim) skip) (bin "<" (call "len" [ v r ]) (v lim)))
        | None -> bin "<" (v c) (v n)
    let loop = [ Assign(c, skip); While(more, load @ pipe 0 @ [ Assign(c, bin "+" (v c) (i64 1L)) ]) ]
    // `xs/+ * & | ^` (or `xs/{$+_}`) of nothing is the op's identity (the typer picks it from $'s type)
    let emptyFold =
        match term.kind, term.body |> List.filter (function Line _ -> false | _ -> true) with
        | IFold, [ Do { k = Bin(op, { k = Var a }, { k = Var el }) } ]
            when a = term.acc && el = term.el && List.contains op [ "+"; "*"; "&"; "|"; "^" ] ->
            [ when' (v first) [ Assign(term.acc, call "foldid" [ mk (Str op); v term.acc ]) ] ]
        | _ -> [ when' (v first) [ Do(call "panic" [ mk (Str "fold of empty collection") ]) ] ]
    let limited e = match limit with Some _ -> call "take" [ e; v lim ] | None -> e
    match term.kind with
    | IMap -> seq (pre @ init @ loop) (limited (v r))
    | IFilter when count -> seq (pre @ init @ loop) (v r)
    | IFilter when graph -> seq (pre @ init @ loop) (call "subgraph" [ v s; call "keys" [ v r ] ])
    | IFilter -> seq (pre @ init @ loop) (limited (if resTy = TStr then call "join" [ v r; mk (Str "") ] else v r))
    | IFold -> seq (pre @ init @ loop @ emptyFold) (v term.acc)
    | IScan -> seq (pre @ init @ loop @ emptyFold) (v r)
    | IEach -> seq (pre @ init @ loop) skip

/// `a op b` with an array on the left: elementwise, right side either scalar or array.
let broadcast op (a: E) (b: E) (bIsArr: bool) =
    let y = Desugar.fresh "y"
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    let rhs = if bIsArr then mk (Index(v y, v ix)) else v y
    let map = mk (Iter { kind = IMap; src = a; el = el; ix = ix; acc = acc; body = [ Do(bin op (v el) rhs) ]; post = [] })
    if not bIsArr then seq [ Assign(y, b) ] map
    else // a longer `a` fails indexing `y`; a shorter one is caught after the loop (checked after, so `a` still fuses)
        let r = Desugar.fresh "r"
        let mismatch = mk (If(bin "!=" (call "len" [ v r ]) (call "len" [ v y ]), seq [ Do(call "panic" [ mk (Str "array length mismatch") ]) ] (i64 0L), i64 0L))
        seq [ Assign(y, b); Assign(r, map); Do mismatch ] (v r)

/// Array element conversion `[a] -> [b]` for a scalar target type.
let convArr (a: E) (castName: string) =
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    mk (Iter { kind = IMap; src = a; el = el; ix = ix; acc = acc; body = [ Do(call castName [ v el ]) ]; post = [] })
