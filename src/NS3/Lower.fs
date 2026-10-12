/// Type-directed rewrites into plain core. The typer calls these once it knows an operand's type,
/// then types the produced (untyped) core like any other code. Typed sub-expressions pass through unchanged.
module Lower

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

/// Map/filter/fold/scan/each as an index loop over an array snapshot of the source.
let iter (it: Iter) (srcTy: Ty) : E =
    let s, t, n, c, r, key = Desugar.fresh "s", Desugar.fresh "t", Desugar.fresh "n", Desugar.fresh "c", Desugar.fresh "r", Desugar.fresh "k"
    let isMap = match srcTy with TMap _ -> true | _ -> false
    let folding = it.kind = "fold" || it.kind = "scan"
    let snapshot =
        match srcTy with
        | TArr _ -> v s
        | TStr -> call "chars" [ v s ]
        | TMap _ when folding -> call "vals" [ v s ]
        | _ -> call "arr" [ v s ]
    // a range source is iterated as a counter (lo, lo+step, ...) instead of a materialised array
    let range = match it.src.k with Call("range", [ a; b ]) -> Some(a, b) | _ -> None
    let elemAt (i: E) = match range with Some _ -> bin "+" (v s) (bin "*" i (v t)) | None -> mk (Index(v t, i))
    let pre =
        match range with
        | Some(a, b) ->
            let hi = Desugar.fresh "hi"
            [ Assign(s, a); Assign(hi, b)
              Assign(t, mk (If(bin "<=" (v s) (v hi), i64 1L, i64 -1L)))
              Assign(n, bin "*" (bin "-" (v hi) (v s)) (v t)) ]
        | None -> [ Assign(s, it.src); Assign(t, snapshot); Assign(n, call "len" [ v t ]) ]
    let seed = // folding an empty collection is an error
        Do(mk (If(bin "<" (v n) (i64 1L), call "panic" [ mk (Str "fold of empty collection") ], i64 0L)))
    let load =
        if isMap && not folding then
            [ Assign(key, mk (Index(v t, v c))); Assign(it.ix, v key); Assign(it.el, mk (Index(v s, v key))) ]
        else [ Assign(it.el, elemAt (v c)); Assign(it.ix, v c) ]
    let next = Assign(c, bin "+" (v c) (i64 1L))
    let ss, value = splitBody it.body
    let bodyE = seq ss value
    let loop from (step: S list) = [ Assign(c, i64 from); While(bin "<" (v c) (v n), load @ step @ [ next ]) ]
    let sameKind () =
        match srcTy with
        | TMap _ -> call "mapof" [ mk (Arr []); mk (Arr []) ]
        | TSet _ -> call "set" [ mk (Arr []) ]
        | TQ _ -> call "Q" [ mk (Arr []) ]
        | TPQ _ -> call "PQ" [ mk (Arr []) ]
        | _ -> mk (Arr [])
    let add (x: E) =
        match srcTy with
        | TMap _ -> SetIdx(v r, v key, x)
        | TSet _ | TQ _ | TPQ _ -> Do(bin "+" (v r) x)
        | _ -> Do(call "push" [ v r; x ])
    match it.kind with
    | "map" -> seq (pre @ [ Assign(r, sameKind ()) ] @ loop 0L [ add bodyE ]) (v r)
    | "filter" ->
        let keep = mk (If(bodyE, seq [ add (v it.el) ] (i64 0L), i64 0L))
        let result = if srcTy = TStr then call "join" [ v r; mk (Str "") ] else v r
        seq (pre @ [ Assign(r, sameKind ()) ] @ loop 0L [ Do keep ]) result
    | "fold" -> seq (pre @ [ seed; Assign(it.acc, elemAt (i64 0L)) ] @ loop 1L [ Assign(it.acc, bodyE) ]) (v it.acc)
    | "scan" ->
        let init = [ seed; Assign(it.acc, elemAt (i64 0L)); Assign(r, mk (Arr [])); Do(call "push" [ v r; v it.acc ]) ]
        seq (pre @ init @ loop 1L [ Assign(it.acc, bodyE); Do(call "push" [ v r; v it.acc ]) ]) (v r)
    | _ -> seq (pre @ loop 0L [ Do bodyE ]) (i64 0L) // "each"

let private each src body =
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    mk (Iter { kind = "each"; src = src; el = el; ix = ix; acc = acc; body = body (v el) })

/// `a op b` with an array on the left: elementwise, right side either scalar or array.
let broadcast op (a: E) (b: E) (bIsArr: bool) =
    let y = Desugar.fresh "y"
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    let rhs = if bIsArr then mk (Index(v y, v ix)) else v y
    seq [ Assign(y, b) ] (mk (Iter { kind = "map"; src = a; el = el; ix = ix; acc = acc; body = [ Do(bin op (v el) rhs) ] }))

/// Array element conversion `[a] -> [b]` for a scalar target type.
let convArr (a: E) (castName: string) =
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    mk (Iter { kind = "map"; src = a; el = el; ix = ix; acc = acc; body = [ Do(call castName [ v el ]) ] })

/// Set union/difference/intersection; mutates and returns the left set.
let setOp op (a: E) (b: E) =
    let s, o = Desugar.fresh "s", Desugar.fresh "o"
    let body =
        match op with
        | "|" | "+" -> [ Assign(s, a); Do(each b (fun x -> [ Do(bin "+" (v s) x) ])) ]
        | "-" -> [ Assign(s, a); Do(each b (fun x -> [ Do(bin "-" (v s) x) ])) ]
        | _ ->
            [ Assign(s, a); Assign(o, b)
              Do(each (call "arr" [ v s ]) (fun x ->
                  [ Do(mk (If(call "has" [ v o; x ], i64 0L, seq [ Do(bin "-" (v s) x) ] (i64 0L)))) ])) ]
    seq body (v s)
