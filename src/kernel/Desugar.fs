/// Stage 3: surface AST -> core. Removes all syntax sugar that does not need types.
module Desugar

open Ast
open Core
open System.Collections.Generic

let mutable private counter = 0
let mutable private line = 0 // of the statement being desugared, for errors
let fresh (prefix: string) =
    counter <- counter + 1
    $"{prefix}%%{counter}"
/// An id for a block the compiler makes (the parser's ids count up from 1).
let freshId () =
    counter <- counter + 1
    1_000_000_000 + counter

/// Block id -> how many arguments it takes: the highest `_k` its body reads (nested blocks have their own).
let arity = System.Collections.Generic.Dictionary<int, int>()
/// Blocks whose body names `_1 _2 …` (not just `_`): given one tuple by a stdlib callback, they destructure it.
let namesParts = System.Collections.Generic.HashSet<int>()

let rec private arityE (e: E) : int =
    match e.k with
    | Var n when n.Length > 1 && n.[0] = '_' && Seq.forall System.Char.IsDigit n.[1..] -> int n.[1..]
    | Arr xs | Call(_, xs) -> xs |> List.map arityE |> List.fold max 0
    | Bin(_, a, b) | Index(a, b) -> max (arityE a) (arityE b)
    | If(c, a, b) -> max (arityE c) (max (arityE a) (arityE b))
    | Iter it -> max (arityE it.src) (arityS it.body)
    | Seq(ss, x) -> max (arityS ss) (arityE x)
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

/// `x'` state of the innermost loop being desugared: x -> [x' ; x''; ...] (variables `prev'^n`), and where their
/// zero initialisations go (before the statement holding the loop, so fused chains stay plain `Iter`s).
type private Prev = { chains: System.Collections.Generic.Dictionary<string, ResizeArray<string>>; inits: ResizeArray<S> }
/// The initialisations collected for the statement being desugared.
let mutable private inits = ResizeArray<S>()
let mutable private loop: Prev option = None
/// `prev'^n` -> the variable it trails, so the typer can give it that type before the shift assigns it.
let prevOf = System.Collections.Generic.Dictionary<string, string>()
let private newLoop () = { chains = System.Collections.Generic.Dictionary(); inits = inits }

/// The variable holding u as it was k iterations ago in the innermost loop.
let private prevVar (shown: string) (u: string) (k: int) =
    match loop with
    | None -> fail line $"'{shown}' (previous iteration) needs an enclosing loop: a block over a collection or a ~{{ loop"
    | Some p ->
        let ch = match p.chains.TryGetValue u with | true, c -> c | _ -> (let c = ResizeArray() in p.chains.[u] <- c; c)
        while ch.Count < k do
            counter <- counter + 1
            let v = $"prev'^{counter}" // not a temp (no `%`): it owns its value like a user variable
            prevOf.[v] <- (if ch.Count = 0 then u else ch.[ch.Count - 1])
            ch.Add v
            p.inits.Add(Assign(v, mk (Call("zero", []))))
        ch.[k - 1]

/// End of an iteration: shift every chain by one (`x'' = x'; x' = x`).
let private shifts (p: Prev) =
    [ for KeyValue(u, ch) in p.chains do
          for i in ch.Count - 1 .. -1 .. 1 -> Assign(ch.[i], mk (Var ch.[i - 1]))
          yield Assign(ch.[0], mk (Var u)) ]

/// Implicit-name renaming: `_ @ $` (and `_#`, `@`, `_blk`) -> the variables of the innermost block.
type Env = Map<string, string>

/// What a feature's desugaring sees: the kernel's own translation functions.
type Api =
    { expr: Env -> Expr -> E
      block: Env -> Stmt list -> S list
      /// a loop over src binding `_ @ $` in the body (made from the extended env)
      iter: Env -> IterKind -> E -> (Env -> S list) -> E
      /// a block assigning `i` keeps it a variable, not the index
      ownsI: Stmt list -> Env -> Env
      /// runs f outside the enclosing loop's `x'` state (code that runs per element but not in that loop)
      outsideLoop: (unit -> E) -> E
      line: unit -> int }

/// Feature constructs: `EExt(name, operands, blocks)` -> core, by name.
let ext = Dictionary<string, Api -> Env -> Expr list -> Stmt list list -> E>()

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
    | EVar n when n.EndsWith "'" ->
        let b = n.TrimEnd '\''
        match (ex (EVar b)).k with
        | Var u -> mk (Var(prevVar n u (n.Length - b.Length)))
        | Index({ k = Var u }, i) -> mk (Index(mk (Var(prevVar n u (n.Length - b.Length))), i)) // `_1'` in an iter block
        | _ -> fail line $"'{n}': ' (previous iteration) only follows a variable name"
    | EVar n when n.Length > 1 && n.[0] = '_' && n.[1] = '0' -> fail line $"'{n}' is not a name: block arguments and element parts count from _1"
    | EVar n when n.Length > 1 && n.[0] = '_' && env.ContainsKey "_#" -> // `_1 _2` in an iter block: element parts
        mk (Index(mk (Var env.["_#"]), mk (Int(int64 n.[1..] - 1L))))
    // `i` in an iter block is its index unless a variable `i` exists: the typer picks (`i|` ix i)
    | EVar "i" when not (env.ContainsKey "i") && env.ContainsKey "@" -> mk (Call("i|", [ mk (Var env.["@"]); mk (Var "i") ]))
    | EVar n when n.Length > 1 && n.[0] = '_' && Seq.forall System.Char.IsDigit n.[1..] && env.ContainsKey "_blk" ->
        namesParts.Add(int env.["_blk"]) |> ignore
        mk (Var n)
    | EVar n -> mk (Var(defaultArg (Map.tryFind n env) n))
    | EArr xs -> mk (Arr(List.map ex xs))
    | ETuple xs -> call "tuple" xs
    | EBin("**", EArr [ x ], n) -> call "repeat" [ x; n ] // [x]**n: n copies of x; other arrays are repeated in Typer.typeBin
    | EMapLit ps -> mk (Call("mapof", [ mk (Arr(ps |> List.map (fst >> ex))); mk (Arr(ps |> List.map (snd >> ex))) ]))
    | ERange(a, b) -> call "range" [ a; b ]
    | EBin(op, a, b) -> mk (Bin(op, ex a, ex b))
    | EUn(op, x) -> call (match op with "-" -> "neg" | "!" -> "not" | "!b" -> "notbool" | _ -> "len") [ x ]
    | ETern(c, a, b) -> mk (If(ex c, ex a, ex b))
    | ECall(f, _) when f = callMark + "ret" -> fail line "ret: only as a statement of a block, `ret: v` or `cond ? ret: v`"
    | ECall(f, args) when f.StartsWith callMark || f.StartsWith nameMark || f.StartsWith negMark -> // a block param may shadow the function
        let n = f.Substring 1
        call (f.Substring(0, 1) + defaultArg (Map.tryFind n env) n) args
    | ECall(f, args) -> call f args
    | EIndex(x, ERange(a, b)) -> call "slice" [ x; a; b ]
    | EIndex(x, i) -> mk (Index(ex x, ex i))
    | EHas(x, k) -> call "has" [ x; k ]
    | EIter(kind, src, body) -> iter (ownsI body env) kind (ex src) (fun env' -> block env' body)
    | EBlock(id, ps, body) ->
        // implicit names belong to the innermost block; named params of enclosing blocks stay visible.
        // A param is a fresh local copied from its `_i`, so assigning it never touches an outer variable.
        let outer = env |> Map.filter (fun k _ -> not (k.StartsWith "_" || k = "@" || k = "$"))
        let env' = List.concat ps |> List.filter ((<>) "_") |> List.fold (fun (m: Map<_, _>) p -> m.Add(p, $"{p}'{id}")) (outer.Add("_", "_1").Add("_blk", string id))
        let bind =
            ps |> List.mapi (fun i group ->
                let arg = mk (Var $"_{i + 1}")
                match group with
                | [ p ] -> [ Assign($"{p}'{id}", arg) ]
                | _ ->
                    group |> List.indexed |> List.filter (snd >> (<>) "_")
                    |> List.map (fun (k, p) -> Assign($"{p}'{id}", mk (Index(arg, mk (Int(int64 k)))))))
            |> List.concat
        let saved = loop
        loop <- None // a block's body is its own: `x'` there needs a loop inside it
        let body = bind @ block env' body
        loop <- saved
        arity.[id] <- arityS body
        mk (Fn(id, body))
    | EExt(name, xs, blocks) ->
        match ext.TryGetValue name with
        | true, f -> f (api ()) env xs blocks
        | _ -> fail line $"no feature handles '{name}'"
    | EGroup body ->
        match List.rev (block env body) with
        | Do last :: rest -> mk (Seq(List.rev rest, last))
        | _ -> fail line "empty group: ( ) needs a value"

and private api () : Api =
    { expr = expr
      block = block
      iter = iter
      ownsI = ownsI
      outsideLoop = fun f -> (let saved = loop in loop <- None; let r = f () in loop <- saved; r)
      line = fun () -> line }

/// A block assigning `i` keeps it a variable, not the index.
and private ownsI body (env: Map<string, string>) =
    let isI lv = lv = EVar "i"
    if body |> List.exists (function SAssign(lvs, _) -> List.exists isI lvs | SCompound(_, lv, _) -> isI lv | _ -> false)
    then env.Add("i", "i") else env

and private iter env kind src (mkBody: Map<string, string> -> S list) =
    let el, ix, acc = fresh "_", fresh "@", fresh "$"
    let env' = env.Add("_", el).Add("_#", el).Add("@", ix).Add("$", acc)
    let saved, p = loop, newLoop ()
    loop <- Some p
    let body = mkBody env'
    loop <- saved
    mk (Iter { kind = kind; src = src; el = el; ix = ix; acc = acc; body = body; post = shifts p })

/// `cond ? ret: v` leaves the block with v: the statements after it become the else branch.
/// `ret: v` alone ends the block with v.
and private guards (body: Stmt list) =
    let ret = callMark + "ret"
    let value vs = match vs with [ v ] -> v | vs -> ETuple vs
    match body with
    | SExpr(ETern(c, EGroup [ SExpr(ECall(r, vs)); SExpr(EInt 0L) ], EInt 0L) as t) :: rest when r = ret ->
        let e =
            if rest |> List.forall (function SLine _ -> true | _ -> false) then ETern(c, EGroup [ SExpr(value vs); SExpr(EInt 0L) ], EInt 0L)
            else ETern(c, value vs, EGroup(guards rest))
        posOf t |> Option.iter (setPos e)
        [ SExpr e ]
    // bare `ret` (no value): skip the rest of the block, whose value is then 0 (`@ > d@_ ? ret` in a draining queue loop)
    | SExpr(ETern(c, EGroup [ SExpr(EVar "ret"); SExpr(EInt 0L) ], EInt 0L) as t) :: rest ->
        let e = ETern(c, EInt 0L, EGroup(guards rest @ [ SExpr(EInt 0L) ]))
        posOf t |> Option.iter (setPos e)
        [ SExpr e ]
    | SExpr(EVar "ret") :: _ -> [ SExpr(EInt 0L) ]
    | SExpr(ECall(r, vs)) :: _ when r = ret -> [ SExpr(value vs) ]
    | s :: rest -> s :: guards rest
    | [] -> []

/// A block's value is its last statement; make sure that statement is a `Do`.
and private block env body =
    let ss = stmts env (guards body)
    match ss |> List.filter (function Line _ -> false | _ -> true) |> List.tryLast with
    | Some(Do _) -> ss
    | Some(Assign(x, _)) -> ss @ [ Do(mk (Var x)) ]
    | _ -> ss @ [ Do(mk (Int 0L)) ]

and private stmts env (body: Stmt list) : S list = body |> List.collect (stmt env)

and private stmt env (s: Stmt) : S list =
    let saved = inits
    inits <- ResizeArray()
    let ss = stmt1 env s
    let pre = List.ofSeq inits
    inits <- saved
    pre @ ss

and private stmt1 env (s: Stmt) : S list =
    let ex = expr env
    // a value that may run user code (which can reassign x) is computed before `x@i` is located
    let rec plain (e: E) =
        match e.k with
        | Int _ | Float _ | Str _ | Bool _ | Var _ | Call("zero", []) -> true
        | Bin(_, a, b) | Index(a, b) -> plain a && plain b
        | _ -> false
    let rec root lv = match lv with EIndex(x, _) -> root x | EVar n when n.EndsWith "'" -> Some n | _ -> None
    let assign lv (value: E) =
        match lv with
        | _ when (root lv).IsSome -> fail line $"cannot assign to '{(root lv).Value}': a previous-iteration value is read-only"
        | EVar n -> [ Assign(defaultArg (Map.tryFind n env) n, value) ]
        | EIndex(x, i) when plain value -> [ SetIdx(ex x, ex i, value) ]
        | EIndex(x, i) ->
            let t = fresh "t"
            [ Assign(t, value); SetIdx(ex x, ex i, mk (Var t)) ]
        | _ -> fail line "cannot assign to this expression"
    match s with
    | SLine n -> (line <- n; [ Line n ])
    | SExpr e -> [ Do(ex e) ]
    | SWhile(c, body) ->
        let n0 = inits.Count
        let c = ex c
        let condInits = List.ofSeq inits |> List.skip n0 // loops in the condition restart on every test
        let saved, p = loop, newLoop ()
        loop <- Some p
        let body = stmts env body
        loop <- saved
        [ While(c, body @ shifts p @ condInits) ]
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

let mutable debug = false

/// e with kind k, keeping e's source position (for error columns).
let rebuild (e: E) (k: K) =
    let r = { e with k = k }
    posOf e |> Option.iter (setPos r)
    r

/// e with f on each direct subexpression and fs on each nested statement list.
let mapE (f: E -> E) (fs: S list -> S list) (e: E) : E =
    let k =
        match e.k with
        | Arr xs -> Arr(List.map f xs)
        | Bin(op, a, b) -> Bin(op, f a, f b)
        | If(c, a, b) -> If(f c, f a, f b)
        | Call(g, xs) -> Call(g, List.map f xs)
        | Index(x, i) -> Index(f x, f i)
        | Iter it -> Iter { it with src = f it.src; body = fs it.body; post = fs it.post }
        | Fn(id, body) -> Fn(id, fs body)
        | Conv x -> Conv(f x)
        | Seq(ss, x) -> Seq(fs ss, f x)
        | k -> k
    rebuild e k

let mapS (f: E -> E) (fs: S list -> S list) (s: S) : S =
    match s with
    | Assign(n, e) -> Assign(n, f e)
    | SetIdx(x, i, v) -> SetIdx(f x, f i, f v)
    | While(c, body) -> While(f c, fs body)
    | Do e -> Do(f e)
    | Line _ -> s

/// Calls fe on every expression and fs on every statement nested in ss / e.
let rec visitSS (fe: E -> unit) (fs: S -> unit) (ss: S list) =
    for s in ss do
        fs s
        mapS (visitE fe fs) (fun b -> visitSS fe fs b; b) s |> ignore
and visitE fe fs (e: E) : E =
    fe e
    mapE (visitE fe fs) (fun b -> visitSS fe fs b; b) e

/// Whole-program core passes, run in order after desugaring: (order, name, pass).
let passes = ResizeArray<int * string * (S list -> S list)>()

let desugar (prog: Stmt list) : S list =
    arity.Clear(); namesParts.Clear()
    prevOf.Clear()
    loop <- None
    let core = stmts Map.empty prog
    passes |> Seq.sortBy (fun (o, _, _) -> o) |> Seq.fold (fun c (_, _, pass) -> pass c) core
