/// Stage 3: surface AST -> core. Removes all syntax sugar that does not need types.
module Desugar

open Ast
open Core

let mutable private counter = 0
let fresh (prefix: string) =
    counter <- counter + 1
    sprintf "%s%%%d" prefix counter

/// `env` renames the implicit names `_ @ $` to the variables of the innermost block.
let rec private expr (env: Map<string, string>) (e: Expr) : E =
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
    | EVar n -> mk (Var(defaultArg (Map.tryFind n env) n))
    | EArr xs -> mk (Arr(List.map ex xs))
    | ETuple xs -> call "tuple" xs
    | EBin("*", EArr [ x ], n) -> call "repeat" [ x; n ] // [x]*n repeats; other array*int broadcasts
    | EMapLit ps -> mk (Call("mapof", [ mk (Arr(ps |> List.map (fst >> ex))); mk (Arr(ps |> List.map (snd >> ex))) ]))
    | ECollLit(kind, xs) -> mk (Call(kind, [ mk (Arr(List.map ex xs)) ]))
    | ERange(a, b) -> call "range" [ a; b ]
    | EBin(op, a, b) -> mk (Bin(op, ex a, ex b))
    | EUn(op, x) -> call (match op with "-" -> "neg" | "~" -> "not" | _ -> "len") [ x ]
    | ETern(c, a, b) -> mk (If(ex c, ex a, ex b))
    | ECall(f, args) -> call f args
    | EIndex(x, ERange(a, b)) -> call "slice" [ x; a; b ]
    | EIndex(x, i) -> mk (Index(ex x, ex i))
    | EHas(x, k) -> call "has" [ x; k ]
    | EIter(kind, src, body) -> iter env kind (ex src) (fun env' -> block env' body)
    | EIterOp(kind, src, op) ->
        iter env kind (ex src) (fun (env': Map<string, string>) ->
            let acc, el = mk (Var env'.["$"]), mk (Var env'.["_"])
            let body =
                match op with
                | ">" -> If(mk (Bin(">", acc, el)), acc, el)
                | "<" -> If(mk (Bin("<", acc, el)), acc, el)
                | op -> Bin(op, acc, el)
            [ Do(mk body) ])
    | EBlock(id, body) -> mk (Fn(id, block (Map [ "_", "_1" ]) body))

and private iter env kind src (mkBody: Map<string, string> -> S list) =
    let el, ix, acc = fresh "_", fresh "@", fresh "$"
    let env' = env.Add("_", el).Add("@", ix).Add("$", acc)
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
    let assign lv (value: E) =
        match lv with
        | EVar n -> Assign(defaultArg (Map.tryFind n env) n, value)
        | EIndex(x, i) -> SetIdx(ex x, ex i, value)
        | _ -> fail 0 "bad assignment target"
    match s with
    | SLine n -> [ Line n ]
    | SExpr e -> [ Do(ex e) ]
    | SWhile(c, body) -> [ While(ex c, stmts env body) ]
    | SAssign([ lv ], [ e ]) -> [ assign lv (ex e) ]
    | SAssign(lvs, [ e ]) ->
        let t = fresh "t"
        Assign(t, ex e) :: (lvs |> List.mapi (fun i lv -> assign lv (mk (Index(mk (Var t), mk (Int(int64 i)))))))
    | SAssign(lvs, es) when lvs.Length = es.Length ->
        let temps = es |> List.map (fun _ -> fresh "t")
        List.map2 (fun t e -> Assign(t, ex e)) temps es
        @ List.map2 (fun lv t -> assign lv (mk (Var t))) lvs temps
    | SAssign _ -> fail 0 "assignment count mismatch"
    | SCompound(op, lv, e) -> [ assign lv (mk (Bin(op, ex lv, ex e))) ]

let desugar (prog: Stmt list) : S list = stmts Map.empty prog
