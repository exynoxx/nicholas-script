/// Stage 4: core -> typed core. Infers types, inserts conversions (left-operand rule),
/// resolves scopes/captures, monomorphises function blocks, and lowers type-directed forms via Lower.
module Typer

open System.Collections.Generic
open Ast
open Core

type Frame = { parent: Frame option; vars: Dictionary<string, string>; inst: Inst option }

and Inst =
    { name: string
      mutable parms: (string * Ty) list
      mutable caps: string list
      mutable ret: Ty
      mutable body: S list
      mutable result: E
      owned: HashSet<string>
      calls: HashSet<string>
      /// parameters the callee only reads: callers lend them without a retain (set by Opt)
      borrowed: HashSet<string> }

type Program =
    { main: S list
      globals: (string * Ty) list
      insts: Inst list
      varTy: Dictionary<string, Ty> }

let private varTy = Dictionary<string, Ty>()
let private globals = ResizeArray<string>()
let private fnDefs = Dictionary<int, S list * Frame>()
let private insts = Dictionary<string, Inst>()
let private instList = ResizeArray<Inst>()
let private global0 = { parent = None; vars = Dictionary(); inst = None }
let mutable private cur = global0
let mutable private line = 0
/// Column of the node being typed (0: unknown, e.g. in generated code)
let mutable private col = 0
/// Type of the then-branch while typing an else-branch; resolves `rec + rec` in recursive functions.
let mutable private expect: Ty option = None
/// The `x op e` of the statement `x = x op e` (also `x op= e` and a fold step): a set operator there updates x in place.
let mutable private inPlace: E option = None
let mutable private counter = 0
/// Blocks wrapped in `memo:`, and the cache variable of every memoized instance.
let private memos = HashSet<int>()
let private memoCaches = ResizeArray<string>()
/// Element types of empty literals ([] %[] [->]), and the blocks (instance key prefix) whose call fixed an outer one.
let private emptyEls = ResizeArray<Ty>()
let private fixers = HashSet<string>()
let private err msg = failAt line col msg
/// `lazy` functions (block ids), and the sink-passing version of each (see `sinkBody`) per argument count.
let private lazyFns = HashSet<int>()
let private lazySinks = Dictionary<int * int, int>()

let newHole () = THole(ref None)

/// Does type t contain the unbound hole h?
let rec private holeIn (h: Ty) (t: Ty) =
    match resolve h, resolve t with
    | THole a, THole b -> obj.ReferenceEquals(a, b)
    | _, (TArr x | TSet x | TQ x | TPQ x) -> holeIn h x
    | _, TMap(k, v) -> holeIn h k || holeIn h v
    | _, TTree(x, k) -> holeIn h x || (match k with Some(_, kt) -> holeIn h kt | None -> false)
    | _, TTup ts -> List.exists (holeIn h) ts
    | _ -> false
let private typed k t = { k = k; t = t }
/// A type as errors show it: a block is "a block", not its internal fn<id>.
let private showT t = match resolve t with TFn _ -> "a block" | t -> showTy t
let private isScalar t = match resolve t with TInt | TFloat | TStr | TBool -> true | _ -> false

/// Unifies two types, binding holes; when they don't unify, no hole stays bound.
let private unify a b =
    let bound = ResizeArray<Ty option ref>()
    let rec go a b =
        match resolve a, resolve b with
        | THole r1, THole r2 when obj.ReferenceEquals(r1, r2) -> true
        | THole r, t | t, THole r -> (r.Value <- Some t; bound.Add r; true)
        | TArr x, TArr y | TSet x, TSet y | TQ x, TQ y | TPQ x, TPQ y -> go x y
        | TMap(k1, v1), TMap(k2, v2) -> go k1 k2 && go v1 v2
        | TTup xs, TTup ys -> xs.Length = ys.Length && List.forall2 go xs ys
        | TTree(x, None), TTree(y, None) -> go x y
        | TTree(x, Some(f, k1)), TTree(y, Some(g, k2)) -> f = g && go x y && go k1 k2
        | x, y -> x = y
    if go a b then true
    else
        for r in bound do r.Value <- None
        false

// ---------- scopes ----------

let private declare (fr: Frame) (name: string) (ty: Ty) =
    counter <- counter + 1
    let u = $"{name}.{counter}"
    fr.vars.[name] <- u
    varTy.[u] <- ty
    match fr.inst with
    | Some i -> i.owned.Add u |> ignore
    | None -> globals.Add u
    u

/// Finds a variable lexically; a hit in an enclosing function frame becomes a capture of every frame in between.
let private lookup (name: string) =
    let rec find (f: Frame) path =
        match f.vars.TryGetValue name with
        | true, u -> Some(u, f, path)
        | _ -> f.parent |> Option.bind (fun p -> find p (f :: path))
    match find cur [] with
    | Some(u, owner, path) ->
        if owner.inst.IsSome then
            for f in path do
                let i = f.inst.Value
                if not (List.contains u i.caps) then i.caps <- i.caps @ [ u ]
        Some u
    | None -> None

/// `f:args` with f a `lazy` function: Some(block id, f, args).
let private lazyCall (e: E) =
    match e.k with
    | Call(name, args) when not (name.StartsWith nameMark || name.StartsWith negMark) ->
        let f = if name.StartsWith callMark then name.Substring 1 else name
        match lookup f |> Option.map (fun u -> resolve varTy.[u]) with
        | Some(TFn id) when lazyFns.Contains id -> Some(id, f, args)
        | _ -> None
    | _ -> None

/// The source of an untyped map/filter chain (e itself when it is none).
let rec private chainSrc (e: E) =
    match e.k with
    | Iter it when e.t = TUnk && (it.kind = IMap || it.kind = IFilter) -> chainSrc it.src
    | _ -> e

/// Is e a `lazy` call, or a map/filter chain fed by one?
let private streams (e: E) = e.t = TUnk && (lazyCall (chainSrc e)).IsSome

/// e's chain source replaced by h.
let rec private withChainSrc (e: E) (h: E) =
    match e.k with
    | Iter it when e.t = TUnk && (it.kind = IMap || it.kind = IFilter) -> { e with k = Iter { it with src = withChainSrc it.src h } }
    | _ -> h

let private newIter kind src body =
    let el = Desugar.fresh "_"
    { kind = kind; src = src; el = el; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = body (mk (Var el)); post = [] }

/// The body of a `lazy` function rewritten to hand each element of its result to the block `sink` (a parameter),
/// stopping once the sink returns F; the statements before the result run first. It returns whether the sink
/// still wants more. Result forms: `a ++ b`, `c ? a : b`, `(…; x)`, `[x, y]`, `.flatten` of a map (its body's value
/// streams per element); anything else becomes `%yield` (typeAt), decided by its type.
let private sinkBody (body: S list) (sink: string) : S list =
    let m = Desugar.fresh "more"
    let v n = mk (Var n)
    let zero = mk (Int 0L)
    let guard ss = Do(mk (If(v m, mk (Seq(ss, zero)), zero)))
    let send x = Assign(m, mk (Call(sink, [ x ])))
    let rec emit (e: E) : S list =
        match e.k with
        | Bin("++", a, b) -> emit a @ [ guard (emit b) ]
        | If(c, a, b) -> [ Do(mk (If(c, mk (Seq(emit a, zero)), mk (Seq(emit b, zero))))) ]
        | Seq(ss, x) -> ss @ emit x
        | Arr xs -> xs |> List.mapi (fun i x -> if i = 0 then send x else guard [ send x ])
        | Call("flatten", [ { k = Iter it } as x ]) when x.t = TUnk && it.kind = IMap ->
            match List.rev it.body with
            | Do last :: rest -> [ Do(mk (Iter { it with kind = IEach; body = Do(mk (Call("%while", [ v m ]))) :: List.rev rest @ emit last })) ]
            | _ -> [ Do(mk (Call("%yield", [ e; v m; v sink ]))) ]
        | _ -> [ Do(mk (Call("%yield", [ e; v m; v sink ]))) ]
    match List.rev body with
    | Do last :: rest -> List.rev rest @ [ Assign(m, mk (Bool true)) ] @ emit last @ [ Do(v m) ]
    | _ -> body

/// `memo:{..}`: an instance first looks its arguments up in its own global cache (a map, created at program start).
let private memoize (arity: int) (body: S list) =
    match List.rev body with
    | Do value :: rest ->
        let cache, key, r = Desugar.fresh "memo", Desugar.fresh "k", Desugar.fresh "r"
        memoCaches.Add(declare global0 cache (TMap(newHole (), newHole ())))
        let v n = mk (Var n)
        let args = List.init arity (fun i -> v $"_{i + 1}")
        let k = match args with [ a ] -> a | [] -> mk (Int 0L) | _ -> mk (Call("tuple", args))
        let miss = mk (Seq(List.rev rest @ [ Assign(r, value); SetIdx(v cache, v key, v r) ], v r))
        [ Assign(key, k); Do(mk (If(mk (Call("has", [ v cache; v key ])), mk (Index(v cache, v key)), miss))) ]
    | _ -> body

// ---------- conversions ----------

let private castName t =
    match resolve t with
    | TInt -> "int"
    | TFloat -> "float"
    | TStr -> "str"
    | TBool -> "bool"
    | t -> err ($"no conversion to {showT t}")

/// Generated temporaries (`%` in the name) whose every assignment so far stored a fresh array; a group
/// (a lowered loop) yielding one hands it over.
let private freshTemps = Dictionary<string, bool>()

/// Does `e` build a new array that nothing else references? Variables, elements of containers (and
/// branches or groups yielding one) are shared; a slice or stdlib result of nested arrays shares the inner
/// arrays of its arguments, unless those are fresh themselves.
let rec private fresh (e: E) =
    let nested = match resolve e.t with TArr el -> (match resolve el with TArr _ -> true | _ -> false) | _ -> false
    let freshArg (a: E) = match a.k, resolve a.t with Desc, _ | BlockRef _, _ -> true | _, TArr _ -> fresh a | _, t -> isScalar t
    match e.k with
    | Seq(_, { k = Var u }) -> (match freshTemps.TryGetValue u with | true, f -> f | _ -> false) // a group's own result
    | Var _ | Index _ | Call(("tget" | "peek"), _) -> false
    | Call("slice", _) -> not nested
    | StdCall(sg, args) -> (match sg.ret with Stdlib.SVar _ -> false | _ -> true) && (not nested || List.forall freshArg args)
    | If(_, a, b) -> fresh a && fresh b
    | Seq(_, x) -> fresh x
    | _ -> true

/// Arrays are values: copy when an array that is not fresh is stored somewhere else (only the branches
/// that need it); `local` variables die here, so they are handed over.
let rec private ownIn (local: string -> bool) (e: E) =
    match resolve e.t, e.k with
    | TArr _, _ when fresh e -> e
    | TArr _, Var u when local u -> e
    | TArr _, If(c, a, b) -> typed (If(c, ownIn local a, ownIn local b)) e.t
    | TArr _, Seq(ss, x) -> typed (Seq(ss, ownIn local x)) e.t
    | TArr _, _ -> typed (Call("copy", [ e ])) e.t
    | _ -> e

let private own = ownIn (fun _ -> false)

let rec private tryConv (e: E) (ty: Ty) : E option =
    let a, b = resolve e.t, resolve ty
    if unify a b then Some e
    else
        match a, b with
        | TFn _, _ -> None // a block is not a value: no truthiness or text
        | _ when isScalar a && isScalar b -> Some(typed (Conv e) b)
        | _, TStr | _, TBool -> Some(typed (Conv e) b)
        | TArr _, TArr y when isScalar y -> Some(typeE (Lower.convArr e (castName y)))
        | _ -> None

and private conv (e: E) (ty: Ty) : E =
    match tryConv e ty with
    | Some e -> e
    | None ->
        match resolve e.t with
        | TFn _ when resolve ty = TBool -> err "a block can't be used as bool; to filter or test, write the condition itself: x[_>2], not x[{_>2}]"
        | TFn _ -> err ($"a block can't be used as {showT ty}; to use its result, call it: f:x")
        | _ -> err ($"cannot convert {showT e.t} to {showT ty}")

/// Blocks can't escape, so none can be put into an array (also catches a map whose body is a block: x{{..}}).
and private stored (x: E) : E =
    match resolve x.t with
    | TFn _ -> err "a block can't be stored in an array; to map, write x{_+1}, not x{{_+1}}"
    | _ -> x

/// Blocks can't escape: one can only be called, or passed to a function that takes a block.
and private value (x: E) : E =
    match resolve x.t with
    | TFn _ -> err "a block isn't a value: call it, as in f:x"
    | _ -> x

// ---------- expressions ----------

and typeE (e: E) : E =
    if e.t <> TUnk then e
    else
        match posOf e with
        | Some(l, c) ->
            let saved = line, col
            line <- l
            col <- c
            try typeAt e finally (line <- fst saved; col <- snd saved)
        | None -> typeAt e

and private typeAt (e: E) : E =
        match e.k with
        | Int _ -> typed e.k TInt
        | Float _ -> typed e.k TFloat
        | Str _ -> typed e.k TStr
        | Bool _ -> typed e.k TBool
        | Var name ->
            match lookup name with
            | Some u ->
                // `x'` read before its first shift: it has x's type
                match resolve varTy.[u], Desugar.prevOf.TryGetValue name with
                | THole _ as h, (true, src) when (lookup src).IsSome -> unify h (typeE (mk (Var src))).t |> ignore
                | _ -> ()
                typed (Var u) varTy.[u]
            | None when name = "true" || name = "false" -> err ($"undefined variable '{name}': booleans are T and F")
            | None ->
                let rec names (f: Frame) = Seq.append f.vars.Keys (match f.parent with Some p -> names p | None -> Seq.empty)
                err ($"undefined variable '{name}'" + didYouMean name (names cur))
        | Arr [] ->
            let h = newHole ()
            emptyEls.Add h
            typed (Arr []) (TArr h)
        | Arr xs ->
            let xs = List.map (typeE >> stored) xs
            let el = xs.Head.t
            typed (Arr(xs |> List.map (fun x -> own (conv x el)))) (TArr el)
        | Call("zero", []) ->
            // the zero value of a type known later (Desugar: `x'` before enough iterations); the runtime builds it
            let h = newHole ()
            let sg: Stdlib.Sig = { name = "zero"; parms = []; ret = Stdlib.SVar "T"; csym = "ns_zero"; tvars = [ "T" ]; rdesc = false }
            typed (StdCall(sg, [ typed Desc h ])) h
        | Call("foldid", [ { k = Str op }; acc ]) ->
            // identity of a fold op (Lower.iter: an empty `xs/op`)
            let acc = typeE acc
            match op, resolve acc.t with
            | ("+" | "|" | "^"), TInt -> typed (Int 0L) TInt
            | "*", TInt -> typed (Int 1L) TInt
            | "&", TInt -> typed (Int -1L) TInt
            | "+", TFloat -> typed (Float 0.0) TFloat
            | "*", TFloat -> typed (Float 1.0) TFloat
            | "+", TStr -> typed (Str "") TStr
            | "+", TArr _ -> typed (Arr []) acc.t
            | "&", TBool -> typed (Bool true) TBool
            | ("|" | "^"), TBool -> typed (Bool false) TBool
            | _ -> typeE (mk (Seq([ Do(mk (Call("panic", [ mk (Str "fold of empty collection") ]))) ], acc)))
        | Bin("??", a, b) ->
            // `m ?? d`: a missing key of m reads as d from now on (set in place, returns m)
            let a, b = typeE a, typeE b
            match resolve a.t with
            | TMap(_, v) ->
                (match resolve v with THole _ -> unify v b.t |> ignore | _ -> ())
                stdCall "mapdef" [ a; conv b v ]
            | t -> err $"?? gives a map a default value (m ?? d), not {showT t}"
        | Bin(op, a, b) -> typeBin op (inPlace |> Option.exists (fun x -> obj.ReferenceEquals(x, e))) (typeE a) (typeE b)
        | Call("%own", [ x ]) -> setCopy (typeE x) // a fold's first element becomes the accumulator, which is updated in place
        | If(c, a, b) ->
            let c = conv (typeE c) TBool
            let a = value (typeE a)
            let saved = expect
            match resolve a.t with
            | THole _ -> ()
            | t -> expect <- Some t
            let b = value (typeE b)
            expect <- saved
            match tryConv b a.t with
            | Some b -> typed (If(c, a, b)) a.t
            | None ->
                // branches with unrelated types (e.g. two statement groups) run as a statement whose value is 0
                let asStmt (x: E) = typed (Seq([ Do x ], typed (Int 0L) TInt)) TInt
                typed (If(c, asStmt a, asStmt b)) TInt
        | Call(name, args) when name.StartsWith callMark || name.StartsWith nameMark || name.StartsWith negMark ->
            let f = name.Substring 1
            match lookup f with
            | Some u when (match resolve varTy.[u] with TFn _ -> false | _ -> true) ->
                if name.StartsWith nameMark then typeE { e with k = Var f }
                elif name.StartsWith negMark then typeE { e with k = Call("not", [ mk (Var f) ]) }
                else err ($"'{f.Split('\'').[0]}' is a variable here, not a function")
            | _ when name.StartsWith negMark -> typeE args.Head
            | _ -> typeE { e with k = Call(f, args) }
        // a `lazy` call feeding an ending that stops early streams into it (typeIter); as a plain map stage if needed
        | Call("take", [ x; k ]) when (match x.k with Iter _ -> false | _ -> true) && (lookup "take").IsNone && streams x ->
            match lazyArgs x with
            | x, true -> typeIter (newIter IMap x (fun el -> [ Do el ])) (Some k) false None
            | x, false -> builtin "take" [ typeE x; typeE k ]
        | Index(x, ({ k = (Int _ | Var _) } as i)) when (match x.k with Iter _ -> false | _ -> true) && streams x
                                                        && (match i.k with Int n -> n >= 0L | _ -> true) ->
            match lazyArgs x with
            | x, true -> typeIter (newIter IMap x (fun el -> [ Do el ])) None false (Some i)
            | x, false -> typeIndex (typeE x) (typeE i)
        // `.any` is `{bool:_}/|`, `.all` is `{bool:_}/&`, `@?y` is `{_ = y}/|`: a fold of bools stops at the first T / F
        | Call(("any" | "all") as f, [ x ]) when (lookup f).IsNone && streams x ->
            match lazyArgs x with
            | x, true ->
                let fold = newIter IFold (mk (Iter(newIter IMap x (fun el -> [ Do(mk (Call("bool", [ el ]))) ])))) (fun _ -> [])
                typeE (mk (Iter { fold with body = [ Do(mk (Bin((if f = "any" then "|" else "&"), mk (Var fold.acc), mk (Var fold.el)))) ] }))
            | x, false -> builtin f [ typeE x ]
        | Call("has", [ x; y ]) when streams x ->
            match lazyArgs x with
            | x, true ->
                let t = Desugar.fresh "t"
                let fold = newIter IFold (mk (Iter(newIter IMap x (fun el -> [ Do(mk (Bin("=", el, mk (Var t)))) ])))) (fun _ -> [])
                typeE (mk (Seq([ Assign(t, y) ], mk (Iter { fold with body = [ Do(mk (Bin("|", mk (Var fold.acc), mk (Var fold.el)))) ] }))))
            | x, false -> builtin "has" [ typeE x; typeE y ]
        | Call("%yield", [ e; { k = Var m }; { k = Var sink } ]) ->
            // one part of a `lazy` result (sinkBody): a lazy call passes the sink on; an array's elements go to the sink
            // one at a time (a map/filter chain over an array or int fuses into that loop); anything else is one element
            let send (x: E) = Assign(m, mk (Call(sink, [ x ])))
            match lazyCall e with
            | Some(id, _, args) -> typeE (mk (Seq([ Assign(m, mk (Call("%lazy", mk (Int(int64 id)) :: args @ [ mk (Var sink) ]))) ], mk (Int 0L))))
            | None ->
                let h = chainSrc e
                let th = typeE h
                let fuse = not (obj.ReferenceEquals(e, h)) && (match resolve th.t with TArr _ | TInt -> true | _ -> false)
                let e = if fuse then withChainSrc e th else typeE (withChainSrc e th)
                match fuse, resolve e.t with
                | true, _ | _, TArr _ ->
                    typeE (mk (Iter(newIter IEach e (fun el -> [ Do(mk (Call("%while", [ mk (Var m) ]))); send el ]))))
                | _ -> typeE (mk (Seq([ send e ], mk (Int 0L))))
        | Call("%lazy", { k = Int id } :: args) -> userCall (lazySink (int id) (args.Length - 1)) (List.map (typeE >> own) args)
        | Call("%foldmore", [ { k = Str op }; acc ]) ->
            // Lower.iter fed by a `lazy` call: a fold of bools wants more until the first T (`|`) / F (`&`)
            let acc = typeE acc
            match resolve acc.t with
            | TBool when op = "|" -> typed (Call("not", [ acc ])) TBool
            | TBool -> acc
            | _ -> typed (Bool true) TBool
        | Call("take", [ { k = Iter it }; k ]) when (it.kind = IMap || it.kind = IFilter) && (lookup "take").IsNone ->
            typeIter it (Some k) false None
        // `chain@k` stops after k+1 results, like `(chain.take:k+1)@k`; `.one` is `@0`
        | Index({ k = Iter it }, ({ k = (Int _ | Var _) } as i)) when (it.kind = IMap || it.kind = IFilter) && (lookup "take").IsNone
                                                                     && (match i.k with Int n -> n >= 0L | _ -> true) ->
            typeIter it None false (Some i)
        | Call("one", [ x ]) when (lookup "one").IsNone -> typeE { e with k = Index(x, mk (Int 0L)) }
        | Call("take1", [ x ]) when (lookup "take1").IsNone -> typeE { e with k = Call("take", [ x; mk (Int 1L) ]) }
        | Call("len", [ { k = Iter it } ]) when it.kind = IFilter && (lookup "len").IsNone -> typeIter it None true None
        | Call(name, args) ->
            match lookup name with
            | Some u when (match resolve varTy.[u] with TFn _ -> true | _ -> false) ->
                let (TFn id) = resolve varTy.[u]
                // as many arguments as the block reads, but one is always allowed: a block without `_` is called as `f:0`
                match Desugar.arity.TryGetValue id with
                | true, n when args.Length > max 1 n || args.Length < n -> err ($"'{name.Split('\'').[0]}' takes {n} argument(s) but was given {args.Length}")
                | _ -> ()
                userCall id (List.map (typeE >> own) args)
            | _ -> builtin name (List.map typeE args)
        | Index(x, i) -> typeIndex (typeE x) (typeE i)
        | Iter it -> typeIter it None false None
        | Fn(id, body) ->
            fnDefs.[id] <- (body, cur)
            typed e.k (TFn id)
        | Seq(ss, x) ->
            let ss = typeStmts ss
            let x = typeE x
            typed (Seq(ss, x)) x.t
        | MatchOn(src, t, arms, filter) ->
            let src = typeE src
            match resolve src.t with
            | TArr _ ->
                let it = { kind = (if filter then IFilter else IMap); src = src; el = t; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do arms ]; post = [] }
                typeE (mk (Iter it))
            | t when filter -> err ($"?[ ] filters an array, not {showT t}")
            | _ -> typeE (mk (Seq([ Assign(t, src) ], arms)))
        | Conv _ -> err "unexpected conversion"

/// A chain of map/filter stages feeding `it` becomes one loop; `limit` (from `.take:k`) stops it after k results.
/// `count` (`#` of a filter): over an array, the loop counts the kept elements instead of collecting them.
/// `at` (`chain@k`, k a literal >= 0 or a variable): index the result; a fused chain gets the limit k+1 (k when negative).
and private typeIter (it: Iter) (limit: E option) (count: bool) (at: E option) : E =
    // collect a chain of map/filter stages feeding this one (innermost first)
    let rec chain (it: Iter) acc =
        match it.src.k with
        | Iter inner when it.src.t = TUnk && (inner.kind = IMap || inner.kind = IFilter) -> chain inner (it :: acc)
        | _ -> it :: acc
    let stages = chain it []
    // a `lazy` call feeding a chain whose ending stops early streams into it: the stages run per element, no array
    let lazySrc =
        match lazyCall stages.Head.src with
        | Some(id, f, args) when limit.IsSome || at.IsSome || (Lower.boolFold it).IsSome ->
            let args = args |> List.map (typeE >> own)
            let plain = typeE { stages.Head.src with k = Call(f, args) } // types the call, gives the result type
            let feed fn = mk (Call("%lazy", mk (Int(int64 id)) :: args @ [ fn ]))
            Some(plain, (match resolve plain.t with TArr _ -> Some feed | _ -> None))
        | _ -> None
    let feed = lazySrc |> Option.bind snd
    let src = match lazySrc with Some(plain, _) -> plain | None -> typeE stages.Head.src
    match resolve src.t, expect with
    | THole _, Some t -> unify src.t t |> ignore // a recursive call: its result type is the base case's
    | _ -> ()
    // an int in front of a block is the range 0..n: `5{_*_}`, `10[>6]`, `5/+`
    let src = match resolve src.t with TInt -> builtin "range" [ typed (Int 0L) TInt; src ] | _ -> src
    let limit =
        match at, resolve src.t with
        | Some i, (TArr _ | TStr | TQ _ | TPQ _) ->
            let plus1 = mk (Bin("+", i, mk (Int 1L)))
            Some(match i.k with Int _ -> plus1 | _ -> mk (If(mk (Bin("<", i, mk (Int 0L))), i, plus1)))
        | _ -> limit
    let limit = limit |> Option.map (fun k -> conv (typeE k) TInt)
    let r =
      match resolve src.t with
      | TFloat | TBool | TTup _ | TFn _ as t ->
          err ($"cannot {showKind stages.Head.kind} over {showT t}: it needs an int, array, string, map, set, queue or tree")
      | TArr _ when feed.IsSome -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false feed)
      | TArr _ when count -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) None true None)
      | TArr _ | TStr when stages.Length > 1 || limit.IsSome ->
          typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      // a set chain consumed by a fold/scan is lazy: `s{f}/+` sums every mapped value, it builds no (deduplicating) set
      // a queue drains (pop until empty) into an array, so its chains fuse like an array's
      | TQ _ | TPQ _ -> typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      | TSet _ when stages.Length > 1 && (it.kind = IFold || it.kind = IScan) ->
          typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t) limit false None)
      | _ ->
          // not fusable (maps/sets/queues keep their kind per stage): lower stage by stage
          let r = stages |> List.fold (fun (s: E) st -> typeE (Lower.iter [ { st with src = s } ] (resolve s.t) None false None)) src
          let r = match limit with Some k -> builtin "take" [ r; k ] | None -> r
          if count then builtin "len" [ r ] else r
    match at with Some i -> typeIndex r (typeE i) | None -> r

/// A set that the caller may update in place: copied unless nothing else references it.
and private setCopy (x: E) =
    match resolve x.t with
    | TSet el when not (fresh x) -> stdCall "gunion" [ builtin "set" [ typed (Arr []) (TArr el) ]; x ]
    | _ -> x

and private typeBin op inPlace (a: E) (b: E) : E =
    let isArith = List.contains op [ "+"; "-"; "*"; "/"; "%"; "^^" ]
    let isCmp = List.contains op [ "="; "!="; "<"; ">"; "<="; ">=" ]
    let isBit = List.contains op [ "^"; "<<"; ">>"; ">>>" ]
    match resolve a.t, expect with
    | THole _, Some t -> unify a.t t |> ignore // a recursive call: its result type is the base case's
    | THole _, None when op = "++" ->
        unify a.t (match resolve b.t with TArr _ -> b.t | t -> TArr t) |> ignore
    | THole _, None -> unify a.t b.t |> ignore
    | _ -> ()
    let L = resolve a.t
    let same x = typed (Bin(op, a, conv b L)) L
    match op, L with
    | _ when isCmp -> typed (Bin(op, a, conv b L)) TBool
    | ("&" | "|" | "+" | "-"), TSet _ when (match resolve b.t with TSet _ -> true | _ -> false) ->
        stdCall (match op with "&" -> "ginter" | "-" -> "gdiff" | _ -> "gunion") [ (if inPlace then a else setCopy a); conv b L ]
    | ("&" | "|"), TInt -> same ()
    | _, TInt when isBit -> same ()
    | "^", TBool -> same ()
    | "&", _ -> typed (If(conv a TBool, conv b TBool, typed (Bool false) TBool)) TBool
    | "|", _ -> typed (If(conv a TBool, typed (Bool true) TBool, conv b TBool)) TBool
    | "++", TArr el ->
        match resolve b.t with
        | TArr _ -> typed (Call("concat", [ a; conv b L ])) L
        | TSet e | TQ e | TPQ e when not (unify b.t el) && unify e el -> typed (Call("concat", [ a; stdCall "arr" [ b ] ])) L
        | _ -> typed (Call("append", [ a; own (conv b el) ])) L
    // `s ++ xs`: add every element of a collection (array, set, Q, PQ); `s ++ x` adds one element
    | "++", (TSet el | TQ el | TPQ el) ->
        // element type still unknown (`s = %[]`): a collection operand adds its elements, it is not one element
        let unknown = match resolve el with THole _ -> true | _ -> false
        match resolve b.t with
        | TArr e | TSet e | TQ e | TPQ e when (unknown || not (unify b.t el)) && unify e el ->
            let av, x = declare cur (Desugar.fresh "c") L, Desugar.fresh "_"
            let cv = typed (Var av) L
            let b = match resolve b.t with TQ _ | TPQ _ -> stdCall "arr" [ b ] | _ -> b // iterating a queue would drain it
            let it = { kind = IEach; src = b; el = x; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do(mk (Bin("+", cv, mk (Var x)))) ]; post = [] }
            typed (Seq([ Assign(av, a); Do(typeE (mk (Iter it))) ], cv)) L
        | _ -> typed (Call("add", [ a; own (conv b el) ])) L
    | "+", (TSet el | TQ el | TPQ el) -> typed (Call("add", [ a; own (conv b el) ])) L
    | "-", TSet el -> typed (Call("del", [ a; conv b el ])) L
    | "+", TTree _ -> treeOp "btadd" a b
    | "-", TTree _ -> treeOp "btdel" a b
    | "+", TMap _ -> typed (Call("merge", [ a; conv b L ])) L
    | "-", TMap(k, _) -> typed (Call("del", [ a; conv b k ])) L
    // `**` repeats: `[2,3]**2` = [2,3,2,3], `"ab"**3` = "ababab" (`[x]**n` becomes `repeat` in Desugar)
    | "**", TArr _ -> stdCall "flatten" [ builtin "repeat" [ a; b ] ]
    | "**", TStr -> typed (Bin("*", a, conv b TInt)) TStr
    | "**", (TSet _ | TMap _ | TQ _ | TPQ _) -> stdCall "keep" [ a; conv b TInt ] // `st**F`, `m**0`: empty in place
    | "**", (TInt | TFloat) -> err "** repeats arrays and strings; power is ^^"
    | "/", TStr ->
        match resolve b.t with
        | TStr -> stdCall "split" [ a; b ] // `"a,b"/","` = split:"a,b",","
        | t -> err ($"str / {showT t}: only str / str (split) is defined")
    | _, TArr _ when isArith || isBit ->
        let bIsArr = match resolve b.t with TArr _ -> true | _ -> false
        let rec depth t = match resolve t with TArr e -> 1 + depth e | _ -> 0
        if bIsArr && depth L < depth b.t then
            err ($"{showT L} {op} {showT b.t}: arrays combine element by element, and the right one is nested deeper")
        typeE (Lower.broadcast op a b bIsArr)
    | _, TTup ts when isArith ->
        // elementwise: `(a,b) + (c,d)` is `(a+c, b+d)`; a non-tuple right operand applies to every element
        let ta, tb = Desugar.fresh "t", Desugar.fresh "t"
        let bTup = match resolve b.t with TTup us -> us.Length = ts.Length || err ($"tuple {op}: arities {ts.Length} and {us.Length} differ") | _ -> false
        let at x i = mk (Index(mk (Var x), mk (Int(int64 i))))
        let elems = ts |> List.mapi (fun i _ -> mk (Bin(op, at ta i, (if bTup then at tb i else mk (Var tb)))))
        typeE (mk (Seq([ Assign(ta, a); Assign(tb, b) ], mk (Call("tuple", elems)))))
    | _, (TInt | TFloat) when isArith ->
        match resolve b.t with
        | TArr _ -> err ($"{showT L} {op} {showT b.t}: put the array on the left")
        | _ -> same ()
    | "+", TStr -> same ()
    | "*", TStr -> err "operator * not defined for str: use ** to repeat a string"
    | ("+" | "-" | "*"), TBool -> same ()
    | _, TFn _ -> err ($"operator {op} not defined for a block; to call it, write f:x")
    | _ -> err ($"operator {op} not defined for {showT L}")

and private typeIndex (x: E) (i: E) =
    match resolve x.t with
    | TArr el -> typed (Index(x, conv i TInt)) el
    | TStr -> typed (Index(x, conv i TInt)) TStr
    | TMap(k, v) -> typed (Index(x, conv i k)) v
    | TQ el | TPQ el ->
        match i.k with
        | Int 0L -> typed (Call("peek", [ x ])) el
        | _ -> typed (Index(stdCall "arr" [ x ], conv i TInt)) el
    | TTup ts ->
        match i.k with
        | Int k when abs (int k) < ts.Length + (if k < 0L then 1 else 0) ->
            let k = if k < 0L then int k + ts.Length else int k
            typed (Call("tget", [ x; typed (Int(int64 k)) TInt ])) ts.[k]
        | Int k -> err ($"tuple index {k} is out of range: the tuple has {ts.Length} values (0..{ts.Length - 1})")
        | _ -> err ($"tuple index must be a literal 0..{ts.Length - 1}")
    | t -> err ($"cannot index {showT t}")

and private builtin name (args: E list) : E =
    let call t = typed (Call(name, args)) t
    // a range end that is an array means its length: 0..xs = 0..#xs
    let bound (x: E) = match resolve x.t with TArr _ -> typed (Call("len", [ x ])) TInt | _ -> conv x TInt
    let elemOf (x: E) =
        match resolve x.t with
        | TArr e | TSet e | TQ e | TPQ e | TTree(e, _) -> e
        | TStr -> TStr
        | TMap(k, _) -> k
        | t -> err ($"""{(match name with "len" -> "#" | "has" -> "@?" | n -> n)}: not a collection: {showT t}""")
    match name, args with
    | ("print" | "println"), [ x ] -> typed (Call(name, [ value x ])) x.t
    | ("int" | "float" | "str" | "bool"), [ x ] ->
        conv x (match name with "int" -> TInt | "float" -> TFloat | "str" -> TStr | _ -> TBool)
    | "neg", [ x ] ->
        match resolve x.t with
        | TInt | TFloat -> call x.t
        | TArr _ -> typeE (Lower.broadcast "*" x (typed (Int -1L) TInt) false)
        | TStr -> stdCall "rev" [ x ] // `-"abc"` is "cba"
        | _ -> typed (Call("neg", [ conv x TInt ])) TInt
    | "entries", [ x ] -> (match resolve x.t with TMap _ -> stdCall "items" [ x ] | _ -> x)
    | "not", [ x ] -> typed (Call("not", [ conv x TBool ])) TBool
    | "notbool", [ x ] -> // `!{..}`: the block's value must be a bool
        if unify (resolve x.t) TBool then typed (Call("not", [ x ])) TBool
        else err ($"!{{..}} negates a block that must return bool, but this one returns {showT x.t}")
    | "len", [ x ] -> (elemOf x |> ignore; call TInt)
    | "has", [ c; x ] when (match resolve c.t with TTree _ -> true | _ -> false) -> treeOp "bthas" c x
    | "has", [ c; x ] -> typed (Call("has", [ c; conv x (elemOf c) ])) TBool
    | "range", [ a; b ] -> typed (Call("range", [ bound a; bound b ])) (TArr TInt)
    | "slice", [ x; a; b ] ->
        match resolve x.t with
        | TArr _ | TStr -> typed (Call("slice", [ x; bound a; bound b ])) x.t
        | t -> err ($"cannot slice {showT t}")
    | "mapof", [ ks; vs ] -> call (TMap(elemOf ks, elemOf vs))
    | ("set" | "Q" | "PQ"), [ { t = t } as x ] when (match resolve t with TTup _ -> true | _ -> false) ->
        // `set:(a,b,c)`: the tuple's values (converted to the first one's type) as an array
        let (TTup ts) = resolve t
        let tv = typed (Var(declare cur (Desugar.fresh "t") t)) t
        let (Var u) = tv.k
        let vs = ts |> List.mapi (fun k ty -> conv (typed (Call("tget", [ tv; typed (Int(int64 k)) TInt ])) ty) ts.Head)
        builtin name [ typed (Seq([ Assign(u, x) ], typed (Arr vs) (TArr ts.Head))) (TArr ts.Head) ]
    | ("set" | "Q" | "PQ"), [ xs ] ->
        let el = elemOf xs
        let t = match name with "set" -> TSet el | "Q" -> TQ el | _ -> TPQ el
        typed (Call(name, [ (match resolve xs.t with TArr _ -> xs | _ -> stdCall "arr" [ xs ]) ])) t
    | "BT", xs :: f ->
        // `BT[..]` / `BT:xs` / `BT:xs,{key}`: an empty tree, then each element added with `+`
        let key =
            match f with
            | [] -> None
            | [ f ] -> (match resolve f.t with TFn id -> Some(id, newHole ()) | _ -> err "BT: the second argument must be a key block")
            | _ -> err "BT: expects a collection and an optional key block"
        let t = TTree(elemOf xs, key)
        let tv = typed (Var(declare cur (Desugar.fresh "bt") t)) t
        let fill =
            match xs.k with
            | Arr [] -> []
            | _ ->
                let el = Desugar.fresh "_"
                let it = { kind = IEach; src = xs; el = el; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do(mk (Bin("+", tv, mk (Var el)))) ]; post = [] }
                [ Do(typeE (mk (Iter it))) ]
        let (Var u) = tv.k
        typed (Seq(Assign(u, typed (Call("BT", [])) t) :: fill, tv)) t
    | "push", [ a; x ] ->
        match resolve a.t with
        | TArr el -> typed (Call("push", [ a; own (conv (stored x) el) ])) a.t
        | t -> err ($"push: not an array: {showT t}")
    | "panic", [ msg ] -> call TInt
    | "try", [] -> call TBool
    | "untry", [] -> call TInt
    | "memo", [ { k = Fn(id, _) } as f ] -> (memos.Add id |> ignore; f)
    | "memo", _ -> err "memo: expects a block, as in f = memo:{...}"
    | "lazy", [ { k = Fn(id, _) } as f ] ->
        if cur.inst.IsSome then err "lazy: only a top-level function can be lazy"
        lazyFns.Add id |> ignore
        f
    | ("sum" | "prod"), [ x ] when (match resolve x.t with TArr e -> (match resolve e with TInt | TFloat | THole _ -> false | _ -> true) | _ -> false) ->
        let hint = match resolve x.t with TArr e when (match resolve e with TTup _ -> true | _ -> false) -> "; fold tuples with /+ or /*" | _ -> ""
        err ($"{name}: needs an array of int or float, not {showT x.t}{hint}")
    | ("any" | "all"), [ x ] when (match resolve x.t with TArr e -> (match resolve e with TBool | TInt | TFloat | THole _ -> false | _ -> true) | _ -> true) ->
        err ($"{name}: needs an array of bool, int or float, not {showT x.t}; map it first, e.g. xs{{_ > 0}}.{name}")
    | "tuple", xs when xs.Length <= 9 -> typed (Call("tuple", List.map (value >> own) xs)) (TTup(xs |> List.map (fun x -> x.t)))
    | "tuple", xs -> err ($"a tuple holds at most 9 values, not {xs.Length}; use an array")
    | "destructure", [ x; { k = Int n } ] -> // `a, b = x`
        match resolve x.t with
        | TTup ts when ts.Length <> int n -> err ($"{n} names on the left but the tuple has {ts.Length} values")
        | TInt | TFloat | TBool | TFn _ as t -> err ($"can't destructure {showT t} into {n} names")
        | _ -> x
    | "repeat", [ x; n ] -> typed (Call("repeat", [ own (value x); conv n TInt ])) (TArr x.t)
    | _ when Stdlib.names.Contains name -> stdCall name args
    | _ -> err ($"unknown function '{name}' with {args.Length} argument(s)" + didYouMean name Parser.builtins)

/// A runtime stdlib call, typed from its `//@` signature (see Stdlib.fs). Type variables bind on first
/// use; other arguments convert to the parameter type. The typed node carries one descriptor string per
/// type variable (and the result's, see Stdlib) before the arguments; a block argument becomes a `BlockRef`.
and private stdCall name (args: E list) : E =
    let rec toTy (env: Dictionary<string, Ty>) (p: Stdlib.STy) : Ty =
        match p with
        | Stdlib.SInt -> TInt
        | Stdlib.SFloat -> TFloat
        | Stdlib.SStr -> TStr
        | Stdlib.SBool -> TBool
        | Stdlib.SVar v -> (match env.TryGetValue v with | true, t -> t | _ -> (let h = newHole () in env.[v] <- h; h))
        | Stdlib.SArr t -> TArr(toTy env t)
        | Stdlib.SMap(k, v) -> TMap(toTy env k, toTy env v)
        | Stdlib.SSet t -> TSet(toTy env t)
        | Stdlib.SQ t -> TQ(toTy env t)
        | Stdlib.SPQ t -> TPQ(toTy env t)
        | Stdlib.SBT t -> TTree(toTy env t, None)
        | Stdlib.STup ts -> TTup(List.map (toTy env) ts)
        | Stdlib.SFn _ -> err "nested block types are not supported"
    /// binds unbound variables from the actual type; true when no conversion is needed
    let rec bind (env: Dictionary<string, Ty>) (p: Stdlib.STy) (t: Ty) : bool =
        match p, resolve t with
        | Stdlib.SVar v, t ->
            match env.TryGetValue v with
            | true, b -> resolve b = t
            | _ -> (env.[v] <- t; true)
        | Stdlib.SArr p, TArr t | Stdlib.SSet p, TSet t | Stdlib.SQ p, TQ t | Stdlib.SPQ p, TPQ t | Stdlib.SBT p, TTree(t, _) -> bind env p t
        | Stdlib.SMap(pk, pv), TMap(k, v) -> bind env pk k && bind env pv v
        | Stdlib.STup ps, TTup ts -> ps.Length = ts.Length && List.forall2 (bind env) ps ts
        | Stdlib.SFn _, TFn _ -> true
        | Stdlib.SInt, TInt | Stdlib.SFloat, TFloat | Stdlib.SStr, TStr | Stdlib.SBool, TBool -> true
        | _ -> false
    let candidates = Stdlib.overloads name |> List.filter (fun (sg: Stdlib.Sig) -> sg.parms.Length = args.Length)
    if candidates.IsEmpty then err ($"{name}: no overload takes {args.Length} argument(s)")
    // no overload fits: list them all, e.g. "max takes (T,T), [T] or BT T, not str"
    let mismatch () =
        let shows f xs = match xs with [ x ] -> f x | xs -> "(" + (xs |> List.map f |> String.concat ",") + ")"
        let alts = Stdlib.overloads name |> List.map (fun sg -> shows Stdlib.showSTy sg.parms) |> List.distinct |> List.rev
        let alts = if alts.Length = 1 then alts.Head else String.concat ", " (List.rev alts.Tail) + " or " + alts.Head
        $"{name} takes {alts}, not {shows (fun (a: E) -> showT a.t) args}"
    let exact (sg: Stdlib.Sig) = let env = Dictionary() in List.forall2 (bind env) sg.parms (args |> List.map (fun a -> a.t))
    let sg = candidates |> List.tryFind exact |> Option.defaultValue candidates.Head
    let env = Dictionary<string, Ty>()
    let args =
        List.map2 (fun (p: Stdlib.STy) (a: E) ->
            match p, resolve a.t with
            | Stdlib.SFn(pts, rt), TFn id ->
                let call = userCall id (pts |> List.map (fun pt -> typed (Int 0L) (toTy env pt)))
                match call.k, rt with
                | UserCall(inst, _), Stdlib.SBool when unify call.t TBool -> typed (BlockRef inst) a.t
                | UserCall(inst, _), Stdlib.SVar v ->
                    env.[v] <- call.t
                    typed (BlockRef inst) a.t
                | _ -> err ($"{name}: the block must return {Stdlib.showSTy rt}")
            | Stdlib.SFn _, _ -> err ($"{name} expects a block argument")
            | Stdlib.SBT _, TTree _ -> (bind env p a.t |> ignore; a) // keeps its key block
            | p, _ ->
                let a = value a
                bind env p a.t |> ignore
                let a = match tryConv a (toTy env p) with Some a -> a | None -> err (mismatch ())
                match a.k with Var _ -> own a | _ -> a) sg.parms args // an element argument is only read
    // descriptors are rendered by codegen, once every hole is resolved
    let descs = sg.tvars |> List.map (fun v -> typed Desc (toTy env (Stdlib.SVar v)))
    let ret = toTy env sg.ret
    let rdesc = if sg.rdesc then [ typed Desc ret ] else []
    typed (StdCall(sg, descs @ rdesc @ args)) ret

/// `t + x`, `t - x`, `has(t, x)` on a tree: the runtime gets the element and its key (the element itself
/// when the tree has no key block); `+`/`-` change t in place and yield it.
and private treeOp f (c: E) (x: E) : E =
    let (TTree(el, key)) = resolve c.t
    let tmp (e: E) = typed (Var(declare cur (Desugar.fresh "t") e.t)) e.t
    let x = own (conv x el)
    let tc, tx = tmp c, tmp x
    let k =
        match key with
        | None -> tx
        | Some(id, kt) ->
            let k = userCall id [ tx ]
            if not (unify kt k.t) then err ($"BT: the key block returns {showT k.t}, not {showT kt}")
            k
    let assign (v: E) (e: E) = let (Var u) = v.k in Assign(u, e)
    let call = stdCall f [ tc; k; tx ]
    let result = if f = "bthas" then call else typed (Seq([ Do call ], tc)) c.t
    typed (Seq([ assign tc c; assign tx x ], result)) result.t

/// x `streams`: x with its `lazy` call's arguments typed (typing it again reuses them), and whether the call gives an array.
and private lazyArgs (x: E) =
    let h = chainSrc x
    match lazyCall h with
    | Some(_, f, args) ->
        let call = { h with k = Call(f, args |> List.map (typeE >> own)) }
        withChainSrc x call, (match resolve (typeE call).t with TArr _ -> true | _ -> false)
    | None -> x, false

/// The sink-passing version of `lazy` function `id` called with n arguments: one more parameter, the sink block.
and private lazySink id n =
    match lazySinks.TryGetValue((id, n)) with
    | true, s -> s
    | _ ->
        let body, fr = fnDefs.[id]
        let s = Desugar.freshId ()
        lazySinks.[(id, n)] <- s
        fnDefs.[s] <- (sinkBody body $"_{n + 1}", fr)
        s

and private userCall id (args: E list) : E =
    let tys = args |> List.map (fun a -> resolve a.t)
    let body, defFr = fnDefs.[id]
    // a block nested in a function belongs to that function's instance: it captures that instance's variables
    let outer = match defFr.inst with Some i -> i.name + "/" | None -> ""
    let prefix = outer + string id
    let key = prefix + "(" + (tys |> List.map showTy |> String.concat ",") + ")"
    let unfixed (h: Ty) = match resolve h with THole _ -> true | _ -> false
    let twoTypes () =
        err "a block that fixes the element type of an empty literal ([] %[] [->]) from outside it can't be called at two different types; give the literal its type first"
    let inst =
        match insts.TryGetValue key with
        | true, i -> i
        | _ ->
            if fixers.Contains prefix then twoTypes ()
            // an empty literal passed as an argument belongs to the call, not to the block
            let before = emptyEls |> Seq.filter (fun h -> unfixed h && not (List.exists (holeIn h) tys)) |> List.ofSeq
            let inst =
                { name = $"f{id}_{instList.Count}"
                  parms = []
                  caps = []
                  ret = newHole ()
                  body = []
                  result = typed (Int 0L) TInt
                  owned = HashSet()
                  calls = HashSet()
                  borrowed = HashSet() }
            let fr = { parent = Some defFr; vars = Dictionary(); inst = Some inst }
            inst.parms <- tys |> List.mapi (fun i t -> declare fr ($"_{i + 1}") t, t)
            insts.[key] <- inst
            instList.Add inst
            let saved, savedLine, savedCol = cur, line, col
            cur <- fr
            let ss =
                try typeStmts (if memos.Contains id then memoize tys.Length body else body)
                with CompileError(l, c, msg) when not (msg.Contains "(when called at line ") ->
                    // the body is fine in general; say which call's argument types broke it
                    let ats = if tys.IsEmpty then "" else " with " + (tys |> List.map showT |> String.concat ", ")
                    raise (CompileError(l, c, $"{msg} (when called at line {showLine savedLine}{ats})"))
            match List.rev ss with
            | Do r :: rest ->
                if lazyFns.Contains id then
                    match resolve r.t with
                    | TArr _ -> ()
                    | t -> err ($"lazy: a lazy function must return an array, not {showT t}")
                // the result is fresh unless it is shared: a dying local is handed over, anything else is copied
                inst.result <- ownIn inst.owned.Contains (conv (value r) inst.ret)
                inst.body <- List.rev rest
            | _ -> err "function body has no value"
            cur <- saved
            line <- savedLine
            col <- savedCol
            // a lazy function's sink version is keyed by its sink, the block that fixes the consumer's result
            if before |> List.exists (unfixed >> not) && not (lazySinks.ContainsValue id) then
                if insts.Keys |> Seq.exists (fun k -> k <> key && k.StartsWith(prefix + "(")) then twoTypes ()
                fixers.Add prefix |> ignore
            inst
    cur.inst |> Option.iter (fun c -> c.calls.Add inst.name |> ignore)
    typed (UserCall(inst.name, args)) inst.ret

// ---------- statements ----------

and private typeStmt (s: S) : S =
    match s with
    | Line n -> (if n <> line then (line <- n; col <- 0)); s
    | Do e -> Do(typeE e)
    | While(c, body) ->
        let c = conv (typeE c) TBool
        While(c, typeStmts body)
    | SetIdx(x, i, value) ->
        let x = typeE x
        let i, value = typeE i, typeE value
        match resolve x.t with
        | TArr el -> SetIdx(x, conv i TInt, own (conv value el))
        | TMap(k, v) -> SetIdx(x, own (conv i k), own (conv value v))
        | t -> err ($"cannot assign into {showT t}")
    | Assign(name, e0) ->
        let rec self (x: E) =
            match x.k with
            | Bin(_, { k = Var n }, _) when n = name -> Some x
            | Seq(ss, y) when ss |> List.forall (function Line _ -> true | _ -> false) -> self y
            | _ -> None
        let saved = inPlace
        inPlace <- self e0
        let e = typeE e0
        inPlace <- saved
        let generated = name.Contains '%'
        let store u (e: E) =
            if generated then freshTemps.[u] <- (match freshTemps.TryGetValue u with | true, f -> f | _ -> true) && fresh e
            Assign(u, if generated then e else own e)
        match lookup name with
        | Some u ->
            match e.k with
            | Call("append", [ { k = Var src } as a; x ]) when src = u -> Do(typed (Call("push", [ a; x ])) e.t)
            | _ ->
                match resolve varTy.[u], resolve e.t with
                | TFn a, TFn b when a <> b -> err "a function variable cannot be reassigned to another block"
                | TFn _, _ | _, TFn _ -> store u (conv e varTy.[u])
                | t, et ->
                    match tryConv e t with
                    | Some e -> store u e
                    | None when generated -> store u (conv e t)
                    | None ->
                        // a variable keeps the type of its first assignment; point at the new value
                        posOf e0 |> Option.iter (fun (l, c) -> line <- l; col <- c)
                        err ($"'{name.Split('\'').[0]}' holds {showT t} (from its first assignment), so it can't be assigned {showT et}")
        | None -> store (declare cur name e.t) e

and private typeStmts (ss: S list) =
    // a map whose result is discarded (not the block's last statement) runs as `each`: no result array
    let n = List.length ss
    ss |> List.mapi (fun i s ->
        match s with
        | Do({ k = Iter it } as e) when i < n - 1 && it.kind = IMap -> typeStmt (Do { e with k = Iter { it with kind = IEach } })
        | _ -> typeStmt s)

// ---------- program ----------

/// Callers must forward the captures of the functions they call.
let private propagateCaptures () =
    let byName = instList |> Seq.map (fun i -> i.name, i) |> dict
    let mutable changed = true
    while changed do
        changed <- false
        for i in instList do
            for c in i.calls do
                for cap in byName.[c].caps do
                    if not (i.owned.Contains cap) && not (List.contains cap i.caps) then
                        i.caps <- i.caps @ [ cap ]
                        changed <- true

let check (prog: S list) : Program =
    varTy.Clear(); globals.Clear(); fnDefs.Clear(); insts.Clear(); instList.Clear(); global0.vars.Clear(); freshTemps.Clear()
    memos.Clear(); memoCaches.Clear(); emptyEls.Clear(); fixers.Clear(); lazyFns.Clear(); lazySinks.Clear()
    cur <- global0
    let main = typeStmts prog
    let caches =
        memoCaches |> Seq.map (fun u ->
            match resolve varTy.[u] with
            | TMap(k, v) as t -> Assign(u, typed (Call("mapof", [ typed (Arr []) (TArr k); typed (Arr []) (TArr v) ])) t)
            | t -> err $"memo cache of type {showT t}") |> List.ofSeq
    let main = caches @ main
    propagateCaptures ()
    { main = main
      globals = globals |> Seq.map (fun g -> g, varTy.[g]) |> List.ofSeq
      insts = List.ofSeq instList
      varTy = varTy }
