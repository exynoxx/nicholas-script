/// Pattern matching. `x ?{ 1 => a; 2, 3 => b; >9 => c; d }` compares x with each arm's values (an arm starting
/// with a comparison tests x with it; `_` is x); `?{ c1 => a; c2 => b; d }` takes the first true condition. The
/// first matching arm wins; `_ => d` (or a last arm without `=>`) is the default; without one it is a ternary chain
/// like `c ? a` (value 0). An array x maps the match over its elements, unless a pattern is an array literal
/// (`[1,2] => …`): then x is matched whole. `xs ?[ 1, 2; >9 ]` keeps the elements matching an arm.
/// `f = ?{ arms }` defines the function `f = { _ ?{ arms } }`.
module Features.Match

open Ast
open Core
open Typer

// ---------- lexer ----------

/// At `?[`: is this `c ?[..] : d`, a ternary whose branch is an array literal? (else a `?[ ]` match filter)
/// Not when the brackets can't hold an array literal (`?[>4]`, `?[1 => T; _]`), nor when an earlier `?` of
/// this statement still waits for its `:` (`c ? xs?[1] : ys`: that `:` is the outer ternary's).
let private ternaryArray (ctx: Lexer.Ctx) =
    let src, n, i = ctx.src, ctx.src.Length, ctx.pos ()
    let mutable j, depth = i + 1, 0
    let mutable fin, arms = false, false
    while j < n && not fin do
        match src.[j] with
        | '"' -> j <- ctx.skipString j
        | '(' | '[' | '{' -> depth <- depth + 1
        | ')' | ']' | '}' -> (depth <- depth - 1; if depth = 0 then fin <- true)
        | ';' when depth = 1 -> arms <- true
        | '=' when depth = 1 && j + 1 < n && src.[j + 1] = '>' -> arms <- true
        | _ -> ()
        if not fin then j <- j + 1
    let f = ctx.skipWs (i + 2)
    let compares = "<>=".IndexOf(ctx.at (f - i)) >= 0 || (ctx.at (f - i) = '!' && ctx.at (f - i + 1) = '=')
    // `?`s at this bracket depth since the statement began that no later `:` answers (`println:` is not one)
    let mutable k, d, pending, colons = ctx.count () - 1, 0, 0, 0
    while k >= 0 && ctx.tok k <> TERM && d >= 0 do
        match ctx.tok k with
        | SYM(")" | "]" | "}") -> d <- d + 1
        | SYM s when s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{" -> d <- d - 1
        | SYM "?" when d = 0 -> if colons > 0 then colons <- colons - 1 else pending <- pending + 1
        | SYM ":" when d = 0 -> colons <- colons + 1
        | _ -> ()
        k <- k - 1
    let k = ctx.skipWs (j + 1)
    k < n && src.[k] = ':' && not arms && not compares && pending <= 0

/// `f = ?[ arms ]` defines a filter function (`f = { _ ?[ arms ] }`)
let private assignedFn (ctx: Lexer.Ctx) =
    let c = ctx.count ()
    c >= 2 && ctx.tok (c - 1) = SYM "=" && (match ctx.tok (c - 2) with ID _ -> true | _ -> false)

let private lex (ctx: Lexer.Ctx) =
    let open' s = (ctx.sym s; ctx.moveTo (ctx.pos () + 2); true)
    match ctx.at 0, ctx.at 1 with
    | '?', '{' -> open' "?{"
    | '?', '[' when (ctx.endsExpr () && not (ternaryArray ctx)) || assignedFn ctx -> open' "?["
    | _ -> false

// ---------- parser ----------

/// The arms after `?{` / `?[` (consumed), for a subject (None: conditions only).
let parse (p: Parser.Parser) (subject: Expr option) (filter: bool) : Expr =
    let closer = if filter then "]" else "}"
    let test () =
        match subject, p.Peek() with
        | Some _, SYM("=" | "!=" | "<" | ">" | "<=" | ">=" as op) -> (p.Advance(); EBin(op, EVar "_", p.Shift()))
        | Some _, _ ->
            // an arm that mentions `_` (outside a nested block) is a condition on the subject: `_%2=0 => …`
            let start = p.Pos
            let e = p.Expr()
            let mutable depth, uses = 0, false
            for j in start .. p.Pos - 1 do
                match p.Tok j with
                | SYM s when s.EndsWith "{" -> depth <- depth + 1
                | SYM "}" -> depth <- depth - 1
                | ID "_" when depth = 0 -> uses <- true
                | _ -> ()
            if uses then e else EBin("=", EVar "_", e)
        | None, _ -> p.Expr()
    let skipTerms () = while p.Peek() = TERM do p.Advance()
    // in `?[ ]` every arm's value is tested for truthiness on its own, so arms may give different types
    let truth r = if filter then ETern(r, EBool true, EBool false) else r
    // an arm's result may be a statement (`5 => c += 1`); its value is then 0. `=> { s1; s2 }` runs several
    // statements in the match's scope (`_` stays the subject); the last expression is the value, else 0
    let result () =
        let start = p.Pos
        if p.IsSym "{" then
            p.Advance()
            let body = p.Block "}"
            match body |> List.filter (function SLine _ -> false | _ -> true) |> List.tryLast with
            | Some(SExpr _) -> truth (EGroup body)
            | _ when filter -> p.ErrAt start "a ?[ ] arm's block must end in a value: truthy keeps the element, falsy drops it"
            | _ -> EGroup(body @ [ SExpr(EInt 0L) ])
        else
            match p.Stmt() with
            | SExpr e -> truth e
            | _ when filter -> p.ErrAt start "a ?[ ] arm must give a value, not a statement: truthy keeps the element, falsy drops it"
            | st -> EGroup [ st; SExpr(EInt 0L) ]
    let arms = ResizeArray<Expr list * Expr>()
    let mutable deflt = None
    skipTerms ()
    while not (p.IsSym closer) do
        if deflt.IsSome then p.Err "the default arm (`_ => …`, or one without '=>') must be the last arm"
        if p.Peek() = ID "_" && p.Tok(p.Pos + 1) = SYM "=>" then
            p.Advance(); p.Advance()
            deflt <- Some(result ())
        elif filter || p.Ahead(SYM "=>") then
            let tests = ResizeArray [ test () ]
            while p.Accept "," do tests.Add(test ())
            if p.Accept "=>" then arms.Add(List.ofSeq tests, result ())
            elif filter then arms.Add(List.ofSeq tests, EBool true)
        else deflt <- Some(result ())
        if p.Peek() = TERM then skipTerms ()
        elif not (p.IsSym closer) then p.Err $"expected '=>', a new arm or '{closer}', found {p.Found()}"
    p.Expect closer
    if arms.Count = 0 && (filter || deflt.IsNone) then p.Err "a match needs at least one arm"
    let deflt = if filter && deflt.IsNone then Some(EBool false) else deflt
    let chain =
        Seq.foldBack (fun (tests, r) acc ->
            let c = List.reduce (fun a b -> EBin("|", a, b)) tests
            Some(match acc with Some e -> ETern(c, r, e) | None -> Parser.onlyIf c r)) arms deflt
    // an array-literal pattern (`[1,2] => …`) matches an array subject as a whole instead of per element
    let whole = arms |> Seq.exists (fst >> List.exists (function EBin(_, _, EArr _) -> true | _ -> false))
    match subject with
    | Some s -> EExt((if filter then "matchfilter" elif whole then "matchwhole" else "match"), [ s; chain.Value ], [])
    | None -> chain.Value

/// `f = ?{ arms }` is `f = { _ ?{ arms } }`
let private fnDef filter (p: Parser.Parser) = EBlock(p.NewId(), [], [ SExpr(parse p (Some(EVar "_")) filter) ])

// ---------- desugar: `%match(subject, element var, arms)`, typed per subject below ----------

let private desugar kind (d: Desugar.Api) (env: Desugar.Env) (xs: Expr list) (_: Stmt list list) =
    let x, arms = xs.[0], xs.[1]
    let t = Desugar.fresh "_"
    let armsE env' = d.expr (Map.add "_" t (Map.add "_#" t env')) arms
    match kind with
    | "whole" ->
        let arms = armsE env
        mk (Seq([ Assign(t, d.expr env x) ], arms))
    | _ ->
        // the arms run per element, but outside the enclosing loop's `x'` shifts
        let arms = d.outsideLoop (fun () -> armsE env)
        let x = d.expr env x
        mk (Call((if kind = "filter" then "%matchfilter" else "%match"), [ x; mk (Var t); arms ]))

let private intercept (e: E) =
    match e.k with
    | Call(("%match" | "%matchfilter") as f, [ src; { k = Var t }; arms ]) ->
        let filter = f = "%matchfilter"
        let src = typeE src
        match resolve src.t with
        | TArr _ ->
            let it = { kind = (if filter then IFilter else IMap); src = src; el = t; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do arms ]; post = [] }
            Some(typeE (mk (Iter it)))
        | t when filter -> err $"?[ ] filters an array, not {showT t}"
        | _ -> Some(typeE (mk (Seq([ Assign(t, src) ], arms))))
    | _ -> None

let feature =
    { Feature.empty with
        name = "match"
        lex = [ lex ]
        prefix = [ "?{", fun p _ -> parse p None false ]
        suffix = [ "?{", (fun p c -> parse p (Some c) false); "?[", fun p c -> parse p (Some c) true ]
        assignRhs = [ "?{", fnDef false; "?[", fnDef true ]
        fnStarts = [ SYM "?{"; SYM "?[" ]
        desugar = [ "match", desugar "map"; "matchfilter", desugar "filter"; "matchwhole", desugar "whole" ]
        perElement = [ "%match"; "%matchfilter" ]
        intercept = [ intercept ] }
