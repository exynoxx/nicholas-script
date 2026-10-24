/// Stage 3: surface AST -> core. Removes all syntax sugar that does not need types.
module Desugar

open Ast
open Core

let mutable private counter = 0
let mutable private line = 0 // of the statement being desugared, for errors
let fresh (prefix: string) =
    counter <- counter + 1
    $"{prefix}%%{counter}"

/// Block id -> how many arguments it takes: the highest `_k` its body reads (nested blocks have their own).
let arity = System.Collections.Generic.Dictionary<int, int>()

let rec private arityE (e: E) : int =
    match e.k with
    | Var n when n.Length > 1 && n.[0] = '_' && Seq.forall System.Char.IsDigit n.[1..] -> int n.[1..]
    | Arr xs | Call(_, xs) -> xs |> List.map arityE |> List.fold max 0
    | Bin(_, a, b) | Index(a, b) -> max (arityE a) (arityE b)
    | If(c, a, b) -> max (arityE c) (max (arityE a) (arityE b))
    | Iter it -> max (arityE it.src) (arityS it.body)
    | Seq(ss, x) -> max (arityS ss) (arityE x)
    | MatchOn(s, _, arms, _) -> max (arityE s) (arityE arms)
    | Conv x -> arityE x
    | _ -> 0

and private arityS (ss: S list) =
    ss
    |> List.map (function
        | Assign(_, e) | Do e -> arityE e
        | SetIdx(x, i, v) -> max (arityE x) (max (arityE i) (arityE v))
        | While(c, body) -> max (arityE c) (arityS body)
        | Line _ -> 0)
    |> List.fold max 0

/// `env` renames the implicit names `_ @ $` to the variables of the innermost block.
/// The core node keeps the source position of the AST node, for typer errors.
let rec private expr (env: Map<string, string>) (e: Expr) : E =
    let c = exprAt env e
    posOf e |> Option.iter (setPos c)
    c

and private exprAt (env: Map<string, string>) (e: Expr) : E =
    let ex = expr env
    let call f args = mk (Call(f, List.map ex args))
    match e with
    | EInt v -> mk (Int v)
    | EFloat v -> mk (Float v)
    | EStr s -> mk (Str s)
    | EBool b -> mk (Bool b)
    | EInterp parts ->
        let parts = List.map ex parts
        let first = match parts.Head.k with Str _ -> parts.Head | _ -> mk (Bin("+", mk (Str ""), parts.Head))
        parts.Tail |> List.fold (fun acc p -> mk (Bin("+", acc, p))) first
    | EVar n when n.Length > 1 && n.[0] = '_' && n.[1] = '0' -> fail line $"'{n}' is not a name: block arguments and element parts count from _1"
    | EVar n when n.Length > 1 && n.[0] = '_' && env.ContainsKey "_#" -> // `_1 _2` in an iter block: element parts
        mk (Index(mk (Var env.["_#"]), mk (Int(int64 n.[1..] - 1L))))
    | EVar n -> mk (Var(defaultArg (Map.tryFind n env) n))
    | EArr xs -> mk (Arr(List.map ex xs))
    | ETuple xs -> call "tuple" xs
    | EBin("**", EArr [ x ], n) -> call "repeat" [ x; n ] // [x]**n: n copies of x; other arrays are repeated in Typer.typeBin
    | EMapLit ps -> mk (Call("mapof", [ mk (Arr(ps |> List.map (fst >> ex))); mk (Arr(ps |> List.map (snd >> ex))) ]))
    | ECollLit(kind, xs) -> mk (Call(kind, [ mk (Arr(List.map ex xs)) ]))
    | ERange(a, b) -> call "range" [ a; b ]
    | EBin(op, a, b) -> mk (Bin(op, ex a, ex b))
    | EUn(op, x) -> call (match op with "-" -> "neg" | "!" -> "not" | "!b" -> "notbool" | _ -> "len") [ x ]
    | ETern(c, a, b) -> mk (If(ex c, ex a, ex b))
    | ECall(f, args) when f.StartsWith callMark || f.StartsWith nameMark || f.StartsWith negMark -> // a block param may shadow the function
        let n = f.Substring 1
        call (f.Substring(0, 1) + defaultArg (Map.tryFind n env) n) args
    | ECall(f, args) -> call f args
    | EIndex(x, ERange(a, b)) -> call "slice" [ x; a; b ]
    | EIndex(x, i) -> mk (Index(ex x, ex i))
    | EHas(x, k) -> call "has" [ x; k ]
    | EIter(kind, src, body) -> iter env kind (ex src) (fun env' -> block env' body)
    | ESort(src, body) ->
        // x{(key, @, _)}.sort{_3}: the index breaks ties, so equal keys keep their order; a map sorts its (k,v) pairs
        let keyed =
            iter env IMap (mk (Call("entries", [ ex src ]))) (fun env' ->
                let ss = block env' body
                let last = ss |> List.findIndexBack (function Do _ -> true | _ -> false)
                ss |> List.mapi (fun j s ->
                    match s with
                    | Do key when j = last -> Do(mk (Call("tuple", [ key; mk (Var env'.["@"]); mk (Var env'.["_"]) ])))
                    | s -> s))
        iter env IMap (mk (Call("sort", [ keyed ]))) (fun env' -> [ Do(mk (Index(mk (Var env'.["_"]), mk (Int 2L)))) ])
    | EIterOp(kind, src, op) ->
        iter env kind (ex src) (fun (env': Map<string, string>) ->
            let acc, el = mk (Var env'.["$"]), mk (Var env'.["_"])
            let body =
                match op with
                | ">" -> If(mk (Bin(">", acc, el)), acc, el)
                | "<" -> If(mk (Bin("<", acc, el)), acc, el)
                | op -> Bin(op, acc, el)
            [ Do(mk body) ])
    | EBlock(id, ps, body) ->
        // implicit names belong to the innermost block; named params of enclosing blocks stay visible.
        // A param is a fresh local copied from its `_i`, so assigning it never touches an outer variable.
        let outer = env |> Map.filter (fun k _ -> not (k.StartsWith "_" || k = "@" || k = "$"))
        let env' = List.concat ps |> List.filter ((<>) "_") |> List.fold (fun (m: Map<_, _>) p -> m.Add(p, $"{p}'{id}")) (outer.Add("_", "_1"))
        let bind =
            ps |> List.mapi (fun i group ->
                let arg = mk (Var $"_{i + 1}")
                match group with
                | [ p ] -> [ Assign($"{p}'{id}", arg) ]
                | _ ->
                    group |> List.indexed |> List.filter (snd >> (<>) "_")
                    |> List.map (fun (k, p) -> Assign($"{p}'{id}", mk (Index(arg, mk (Int(int64 k)))))))
            |> List.concat
        let body = bind @ block env' body
        arity.[id] <- arityS body
        mk (Fn(id, body))
    | EMatch(kind, x, arms) ->
        let t = fresh "_"
        let arms = expr (env.Add("_", t).Add("_#", t)) arms
        if kind = "whole" then mk (Seq([ Assign(t, ex x) ], arms)) else mk (MatchOn(ex x, t, arms, (kind = "filter")))
    | EGroup body ->
        match List.rev (block env body) with
        | Do last :: rest -> mk (Seq(List.rev rest, last))
        | _ -> fail line "empty group: ( ) needs a value"

and private iter env kind src (mkBody: Map<string, string> -> S list) =
    let el, ix, acc = fresh "_", fresh "@", fresh "$"
    let env' = env.Add("_", el).Add("_#", el).Add("@", ix).Add("$", acc)
    mk (Iter { kind = kind; src = src; el = el; ix = ix; acc = acc; body = mkBody env' })

/// A block's value is its last statement; make sure that statement is a `Do`.
and private block env body =
    let ss = stmts env body
    match ss |> List.filter (function Line _ -> false | _ -> true) |> List.tryLast with
    | Some(Do _) -> ss
    | Some(Assign(x, _)) -> ss @ [ Do(mk (Var x)) ]
    | _ -> ss @ [ Do(mk (Int 0L)) ]

and private stmts env (body: Stmt list) : S list = body |> List.collect (stmt env)

and private stmt env (s: Stmt) : S list =
    let ex = expr env
    // a value that may run user code (which can reassign x) is computed before `x@i` is located
    let rec plain (e: E) =
        match e.k with
        | Int _ | Float _ | Str _ | Bool _ | Var _ | Call("zero", []) -> true
        | Bin(_, a, b) | Index(a, b) -> plain a && plain b
        | _ -> false
    let assign lv (value: E) =
        match lv with
        | EVar n -> [ Assign(defaultArg (Map.tryFind n env) n, value) ]
        | EIndex(x, i) when plain value -> [ SetIdx(ex x, ex i, value) ]
        | EIndex(x, i) ->
            let t = fresh "t"
            [ Assign(t, value); SetIdx(ex x, ex i, mk (Var t)) ]
        | _ -> fail line "cannot assign to this expression"
    match s with
    | SLine n -> (line <- n; [ Line n ])
    | SExpr e -> [ Do(ex e) ]
    | SWhile(c, body) -> [ While(ex c, stmts env body) ]
    | SAssign([ lv ], [ e ]) -> assign lv (ex e)
    | SAssign(lvs, [ e ]) ->
        // the typer checks the value has as many parts as there are names
        let t, value = fresh "t", mk (Call("destructure", [ ex e; mk (Int(int64 lvs.Length)) ]))
        posOf e |> Option.iter (setPos value)
        Assign(t, value) ::(lvs |> List.mapi (fun i lv -> assign lv (mk (Index(mk (Var t), mk (Int(int64 i)))))) |> List.concat)
    | SAssign(lvs, es) when lvs.Length = es.Length ->
        let temps = es |> List.map (fun _ -> fresh "t")
        List.map2 (fun t e -> Assign(t, ex e)) temps es
        @ List.concat (List.map2 (fun lv t -> assign lv (mk (Var t))) lvs temps)
    | SAssign(lvs, es) -> fail line $"{lvs.Length} names on the left but {es.Length} values on the right"
    | SCompound("??", EIndex(x, k), v)
    | SCompound("??", x, ETuple [ k; v ]) ->
        // `m@k ??= v`, `m ??= (k, v)`: inserts v only when k is missing; v is not evaluated when k is present
        let m, kv = fresh "m", fresh "k"
        [ Assign(m, ex x)
          Assign(kv, ex k)
          Do(mk (If(mk (Call("has", [ mk (Var m); mk (Var kv) ])), mk (Int 0L), mk (Seq([ SetIdx(mk (Var m), mk (Var kv), ex v) ], mk (Int 0L)))))) ]
    | SCompound("??", _, _) -> fail line "??= inserts into a map: m@k ??= v or m ??= (k, v)"
    | SCompound(op, lv, e) -> assign lv (mk (Bin(op, ex lv, ex e)))

/// Debug mode: after every assignment to a user variable, `println:"line: x = "+x`.
let rec private debugE (e: E) : E =
    let d = debugE
    let k =
        match e.k with
        | Arr xs -> Arr(List.map d xs)
        | Bin(op, a, b) -> Bin(op, d a, d b)
        | If(c, a, b) -> If(d c, d a, d b)
        | Call(f, args) -> Call(f, List.map d args)
        | Index(x, i) -> Index(d x, d i)
        | Iter it -> Iter { it with src = d it.src; body = debugSS it.body }
        | Fn(id, body) -> Fn(id, debugSS body)
        | Conv x -> Conv(d x)
        | Seq(ss, x) -> Seq(debugSS ss, d x)
        | MatchOn(s, t, arms, f) -> MatchOn(d s, t, d arms, f)
        | k -> k
    { e with k = k }

and private debugSS (ss: S list) : S list =
    // a block's parameter copies come before its first line marker
    let mutable line = ss |> List.tryPick (function Line n -> Some n | _ -> None) |> Option.defaultValue 0
    // a named parameter `n` is renamed `n'<block id>`
    let user (n: string) = match n.IndexOf '\'' with -1 -> n | k -> n.Substring(0, k)
    let show n = [ Do(mk (Call("println", [ mk (Bin("+", mk (Str $"{showLine line}: {user n} = "), mk (Var n))) ]))) ]
    ss |> List.collect (fun s ->
        match s with
        | Line n -> (line <- n; [ s ])
        | Assign(n, ({ k = Fn _ } as e)) -> [ Assign(n, debugE e) ]
        | Assign(n, e) -> Assign(n, debugE e) :: (if n.Contains '%' then [] else show n)
        | SetIdx(x, i, v) -> SetIdx(debugE x, debugE i, debugE v) :: (match x.k with Var n when not (n.Contains '%') -> show n | _ -> [])
        | While(c, body) -> [ While(debugE c, debugSS body) ]
        | Do e -> [ Do(debugE e) ])

let mutable debug = false

let desugar (prog: Stmt list) : S list =
    arity.Clear()
    let core = stmts Map.empty prog
    if debug then debugSS core else core
