/// Stage 2: tokens -> surface AST. Recursive descent, one function per precedence level.
module Parser

open System.Collections.Generic
open Ast

/// language primitives plus every function declared in runtime.c (see Stdlib.fs); features add theirs
let builtins = HashSet<string>(Seq.append [ "print"; "println"; "int"; "float"; "str"; "bool"; "ret"; "isint"; "isfloat"; "isstr" ] Stdlib.names)
let private zeroArity = Stdlib.zeroArity
let private compounds = set [ "+="; "-="; "*="; "**="; "/="; "%="; "^^="; "++="; "&="; "|="; "^="; "<<="; ">>="; ">>>="; "??=" ]

/// Keywords of other languages: a statement starting with one gets a hint.
let private foreign =
    let fn = "define a function as f x = x*2 (or f = {_*2})"
    dict [ "if", "NS has no 'if': write cond ? a : b (or cond ? a)"
           "else", "NS has no 'else': write cond ? a : b"
           "for", "NS has no 'for': iterate with a block, e.g. 0..n{println:_} or xs{...}"
           "while", "NS has no 'while' keyword: write cond ~{ body }"
           "return", "NS has no 'return': a block's value is its last expression"
           "def", fn; "fn", fn; "func", fn; "function", fn; "lambda", fn
           "let", "declare a variable by assigning it: x = 1"; "var", "declare a variable by assigning it: x = 1"
           "const", "declare a variable by assigning it: x = 1" ]

/// `c ? a` without `:` runs a only when c holds; its value is 0
let onlyIf c a = ETern(c, EGroup [ SExpr a; SExpr(EInt 0L) ], EInt 0L)

/// Implicit names (_ _1 @ $) are never assigned, so `{_=p}` / `[=p]` compare.
let private assignable (name: string) = not (name.StartsWith "_" || name = "@" || name = "$")

/// In a map block a bare top-level `_ = e` writes the value back (Lower.iter); a parenthesised `(_ = e)` or a
/// section `{=e}` is a comparison, so it is wrapped in a group, which that write-back does not match.
/// `x@%i` wraps around: `x@(i % #x)` (floor modulo, so -1 is the last element)
let private wrapIndex x i = EIndex(x, EBin("%", i, EUn("#", x)))
let compared e = match e with EBin("=", EVar "_", _) -> EGroup [ SExpr e ] | e -> e

/// A filter's last statement is its predicate, so `xs[a@_ = b@_]` compares; earlier statements still assign.
let private predicate (body: Stmt list) =
    match List.rev body with
    | SAssign([ lv ], [ e ]) :: rest -> List.rev (SExpr(EBin("=", lv, e)) :: rest)
    | _ -> body

let private opensBracket (s: string) = s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{"

/// Directory of the file being parsed, and every file already imported (each is included once).
type Imports = { dir: string; seen: HashSet<string> }

type Pat =
    | PName of string
    | PLit of Expr
    | PGroup of bool * Pat list // is an array, items

type Parser(tokens: Token[], fnames: HashSet<string>, shadowed: HashSet<string>, params: HashSet<string>, stdAliases: Dictionary<string, string>, nextId: int ref, imports: Imports) =
    let mutable pos = 0
    /// base-case clauses (`f 0 [a, b] = a`) waiting for f's general definition: arity, guard statements, a modifier on one of them
    let clauses = Dictionary<string, int * Stmt list * string option>()
    /// Can this token start an expression? (then a missing operator or ',' is the likely mistake)
    let startsExpr (t: Tok) =
        match t with
        | INT _ | FLOAT _ | STR _ | BOOL _ | ID _ -> true
        | SYM("(" | "[" | "{") -> true
        | SYM s -> Parser.PrefixHooks.ContainsKey s
        | _ -> false
    let peek () = tokens.[pos].tok
    let line () = tokens.[pos].line
    let advance () = pos <- pos + 1
    let isSym s = peek () = SYM s
    let accept s = if isSym s then (advance (); true) else false
    /// Records token j's position as e's, for typer errors.
    let mark j (e: Expr) = (setPos e (tokens.[j].line, tokens.[j].col); e)
    /// Fails at token j; a missing thing at the end of a line is reported after the line's last token.
    let errAt j msg =
        match tokens.[j].tok with
        | TERM | EOF when j > 0 ->
            let mutable k = j - 1
            while k > 0 && tokens.[k].tok = TERM do k <- k - 1
            if tokens.[k].line = tokens.[j].line then failAt tokens.[j].line tokens.[j].col msg
            else failAt tokens.[k].line -1 msg
        | _ -> failAt tokens.[j].line tokens.[j].col msg
    let err msg = errAt pos msg
    let found () =
        let t = peek ()
        match t with
        | ID kw when foreign.ContainsKey kw -> $"'{kw}'; {foreign.[kw]}"
        | SYM "->" -> "'->': a map literal is [k->v, ...], with no prefix"
        | _ -> describeTok t + (if startsExpr t then " (missing an operator or ','?)" else "")
    let expect s = if not (accept s) then err ($"expected '{s}' but found {found ()}")
    // a builtin name assigned a value (`max = 3`) or used as a parameter is a variable in the rest of its block
    let isFunc name = fnames.Contains name || (builtins.Contains name && not (shadowed.Contains name))
    let markCall (mark: string) f args =
        match stdAliases.TryGetValue f with
        | true, b -> ECall(b, args)
        | _ -> ECall(mark + f, args)
    let aliasStd a f =
        if stdAliases.ContainsKey f || (builtins.Contains f && not (fnames.Contains f) && not (zeroArity.Contains f)) then
            stdAliases.[a] <- (match stdAliases.TryGetValue f with | true, b -> b | _ -> f)
            fnames.Add a |> ignore

    // ---------- feature hooks (filled by Feature.register) ----------

    /// An expression starting with this token (already consumed; the argument is its index): `%[`, `?{`, ...
    /// The key "STR{" is a string literal with interpolated code.
    static member val PrefixHooks: Dictionary<string, Parser -> int -> Expr> = Dictionary<string, Parser -> int -> Expr>() with get
    /// A postfix form after an operand (token consumed): `x${key}`, `x/+`, ...
    static member val PostfixHooks: Dictionary<string, Parser -> Expr -> Expr> = Dictionary<string, Parser -> Expr -> Expr>() with get
    /// A form after a whole operand expression, at ternary level (token consumed): `x ?{ arms }`
    static member val SuffixHooks: Dictionary<string, Parser -> Expr -> Expr> = Dictionary<string, Parser -> Expr -> Expr>() with get
    /// A statement starting with this token (consumed): `?? stmt`
    static member val StmtHooks: Dictionary<string, Parser -> Stmt> = Dictionary<string, Parser -> Stmt>() with get
    /// `f = <token> …` (token consumed) defining a function f: `f = ?{ arms }`
    static member val AssignHooks: Dictionary<string, Parser -> Expr> = Dictionary<string, Parser -> Expr>() with get
    /// Tokens after `f =` that make f a function (besides `{`); a name must be followed by `:` (`f = memo:{…}`)
    static member val FnStarts: HashSet<Tok> = HashSet<Tok>() with get
    /// Function modifiers: `m f a = …`, `m f = {…}` and `m <base case>` wrap the block as `m:{…}`
    static member val Modifiers: HashSet<string> = HashSet<string>() with get

    // ---------- the parser state features may use ----------

    member p.Peek() = peek ()
    member p.Advance() = advance ()
    member p.Accept s = accept s
    member p.IsSym s = isSym s
    member p.Expect s = expect s
    member p.Pos with get () = pos and set v = pos <- v
    member p.Tok j = tokens.[j].tok
    member p.Line() = line ()
    member p.Mark j e = mark j e
    member p.Err msg : 'a = err msg
    member p.ErrAt j msg : 'a = errAt j msg
    member p.Found() = found ()
    member p.NewId() = (nextId.Value <- nextId.Value + 1; nextId.Value)
    member p.IsFunc name = isFunc name
    /// A parser over other tokens that shares this one's names (interpolated code)
    member p.Sub(toks: Token list) = Parser(Array.ofList toks, fnames, shadowed, params, stdAliases, nextId, imports)

    member p.Program() =
        p.DeclareFunctions()
        let body = p.Stmts EOF
        for KeyValue(f, _) in clauses do fail 0 $"base case of '{f}' without a general definition `{f} a b = …` after it"
        if peek () <> EOF then err ($"unexpected {describeTok (peek ())}")
        body

    /// Forward references: every top-level `f a b = …`, `f = {…}` and `f = memo:…` is a function name from the start,
    /// so functions can call ones defined later (mutual recursion).
    member p.DeclareFunctions() =
        let mutable depth = 0
        let mutable stmtStart = true
        for j in 0 .. tokens.Length - 1 do
            if stmtStart && depth = 0 then
                pos <- (match tokens.[j].tok with ID m when Parser.Modifiers.Contains m -> j + 1 | _ -> j) // `memo f a = …`, `lazy f = {…}`
                match p.FnDefAhead(), tokens.[pos].tok with
                | Some(f, _, _), _ -> fnames.Add f |> ignore
                | None, ID f when pos + 2 < tokens.Length && tokens.[pos + 1].tok = SYM "="
                                  && (tokens.[pos + 2].tok = SYM "{" || Parser.FnStarts.Contains tokens.[pos + 2].tok) -> fnames.Add f |> ignore
                | _ -> ()
            match tokens.[j].tok with
            | SYM s when opensBracket s -> depth <- depth + 1
            | SYM(")" | "]" | "}") -> depth <- depth - 1
            | _ -> ()
            stmtStart <- tokens.[j].tok = TERM
        pos <- 0

    member p.Stmts(closer: Tok) : Stmt list =
        let out = ResizeArray<Stmt>()
        // a statement `a = b` whose left side can't be assigned compares; only a block's last one has a use
        let mutable unused = None
        let noop () =
            unused |> Option.iter (fun j ->
                errAt j "can't assign to this: only names, x@i and a, b can be assigned; a comparison here has no effect")
        let mutable go = true
        while go do
            while peek () = TERM do advance ()
            if peek () = closer || peek () = EOF then go <- false
            elif peek () = ID "import" then
                // import "file.ns": splice the file's statements here (path relative to this file)
                advance ()
                match peek () with
                | STR [ Lit file ] ->
                    advance ()
                    let path = System.IO.Path.GetFullPath(System.IO.Path.Combine(imports.dir, file))
                    if not (System.IO.File.Exists path) then errAt (pos - 1) ($"import: file not found: {path}")
                    if imports.seen.Add path then
                        imported.Value.Add(System.IO.Path.GetRelativePath(System.IO.Directory.GetCurrentDirectory(), path))
                        let toks = Lexer.lex (System.IO.File.ReadAllText path) (imported.Value.Count * fileLines + 1) 1
                        let sub = Parser(Array.ofList toks, fnames, shadowed, params, stdAliases, nextId, { imports with dir = System.IO.Path.GetDirectoryName path })
                        out.AddRange(sub.Program())
                | t -> err ($"import expects a file name string, found {describeTok t}")
            else
                noop ()
                let start, at = peek (), pos
                out.Add(SLine(line ()))
                let s = p.Stmt()
                out.Add s
                // `_ = e` in a map body writes the element
                unused <-
                    match s, start with
                    | SExpr(EBin("=", _, _)), ID n when not (assignable n) -> None
                    | SExpr(EBin("=", _, _)), _ -> Some at
                    | _ -> None
                if peek () = TERM then advance ()
                elif peek () <> closer then
                    match start with
                    | ID kw when foreign.ContainsKey kw -> err ($"unexpected {describeTok (peek ())} after '{kw}'; {foreign.[kw]}")
                    | _ when isSym ":" && (match tokens.[pos - 1].tok with ID f -> shadowed.Contains f | _ -> false) ->
                        let (ID f) = tokens.[pos - 1].tok in errAt (pos - 1) $"'{f}' is a variable here, not a function"
                    | _ when isSym ":" && (match tokens.[pos - 1].tok with ID _ -> true | _ -> false) ->
                        let (ID f) = tokens.[pos - 1].tok
                        let hint = didYouMean f (Seq.append fnames builtins)
                        errAt (pos - 1) (if hint <> "" then $"'{f}' is not a function{hint}" else $"'{f}' is not a function: define it with a block, e.g. f = {{_+1}}")
                    | _ -> err ($"expected end of statement, found {found ()}")
        if closer = EOF then noop ()
        List.ofSeq out

    member p.Block(closer: string) =
        let saved = HashSet shadowed
        let body = p.Stmts(SYM closer)
        expect closer
        shadowed.Clear(); shadowed.UnionWith saved
        body

    /// Function section: a body that is just a function name calls it on `_`, so `x{int}` is `x{int:_}`; `x[!ws]` is `x[!ws:_]`;
    /// `x[!{..}]` binds the negated block to a hidden name and calls it on `_`.
    member p.SectionBlock(closer: string) = p.Section(p.Block closer)

    /// `x{{..}}` = `x{_{..}}`: maps each inner array
    member p.MapSection(block: Stmt list) =
        match block with
        | [ SLine l; SExpr(EBlock(_, [], body)) ] when (match List.tryLast body with Some(SExpr(EUn("!b", _))) -> false | _ -> true) ->
            [ SLine l; SExpr(EIter(IMap, EVar "_", p.MapSection body)) ]
        | block -> p.Section block

    member p.Section(block: Stmt list) =
        match block with
        | [ SLine l; SExpr(EVar f) ] when isFunc f -> [ SLine l; SExpr(markCall nameMark f [ EVar "_" ]) ]
        | [ SLine l; SExpr(EBlock(id, [], body) as b) ] when (match List.tryLast body with Some(SExpr(EUn("!b", _))) -> true | _ -> false) ->
            let f = $"not'{id}"
            [ SLine l; SAssign([ EVar f ], [ b ]); SExpr(ECall(callMark + f, [ EVar "_" ])) ]
        | [ SLine _; SExpr(ECall(f, [ EBlock(_, [], body) ])) ] when f.StartsWith negMark -> body
        | body -> body

    member p.Stmt() : Stmt =
        match peek () with
        | SYM s when Parser.StmtHooks.ContainsKey s -> (advance (); Parser.StmtHooks.[s] p)
        | _ ->
        // a modifier: `memo f a b = body` is `f = memo:{body}` with named parameters; also `m f = {…}` and `m <base case>`
        let modAt = pos
        let modifier =
            match peek () with
            | ID m when Parser.Modifiers.Contains m && pos + 2 < tokens.Length && (match tokens.[pos + 1].tok with ID _ -> true | _ -> false) ->
                pos <- pos + 1
                if p.FnDefAhead().IsSome || p.ClauseAhead().IsSome || tokens.[pos + 1].tok = SYM "=" then Some m else (pos <- pos - 1; None)
            | _ -> None
        let wrap fn = match modifier with Some m -> ECall(m, [ fn ]) | None -> fn
        match p.ClauseAhead() with
        | Some(f, pats, eq) -> p.Clause(f, pats, eq, modifier)
        | None ->
        match p.FnDefAhead() with
        | Some(f, ps, eq) ->
            // `f a b = body` is `f = {body}` with a, b naming _1, _2; a `{…}` body holds statements
            let defAt = pos
            pos <- eq + 1
            fnames.Add f |> ignore
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            let saved, savedParams = HashSet shadowed, HashSet params
            for n in List.concat ps do
                if builtins.Contains n then shadowed.Add n |> ignore
                if n <> "_" then params.Add n |> ignore
            let body = if accept "{" then p.Block "}" else [ SLine(line ()); SExpr(p.Expr()) ]
            shadowed.Clear(); shadowed.UnionWith saved
            params.Clear(); params.UnionWith savedParams
            // a modifier on a base case (`memo f 0 = 1`) applies to the whole function
            let wrap = match clauses.TryGetValue f with | true, (_, _, Some m) when modifier.IsNone -> (fun fn -> ECall(m, [ fn ])) | _ -> wrap
            let body =
                match clauses.TryGetValue f with
                | true, (n, gs, _) ->
                    clauses.Remove f |> ignore
                    if n <> List.length ps then errAt defAt $"'{f}' has {List.length ps} parameters but its base cases have {n}"
                    gs @ body
                | _ -> body
            let fn = EBlock(id, ps, body)
            SAssign([ EVar f ], [ wrap fn ])
        | None ->
        match p.AssignAhead() with
        | Some "=" ->
            let lvs = p.LValues()
            advance ()
            match lvs with
            | [ EVar f ] when isSym "{" || (Parser.FnStarts.Contains(peek ()) && (match peek () with ID _ -> tokens.[pos + 1].tok = SYM ":" | _ -> true)) ->
                fnames.Add f |> ignore
            | _ -> ()
            let rhs =
                match lvs, peek () with
                | [ EVar _ ], SYM s when Parser.AssignHooks.ContainsKey s -> (advance (); [ Parser.AssignHooks.[s] p ])
                | _ -> p.ExprList()
            match lvs, rhs with
            | [ EVar f ], [ ECall(g, _) ] when g.StartsWith negMark -> fnames.Add f |> ignore // g = !ws
            | [ EVar f ], [ EBlock _ ] -> fnames.Add f |> ignore // g = !{..}
            | [ EVar a ], [ EVar f ] when stdAliases.ContainsKey f || (isFunc f && not (fnames.Contains f)) -> aliasStd a f
            | [ EVar a ], [ EVar f ] when fnames.Contains f -> fnames.Add a |> ignore
            | _ -> for lv in lvs do (match lv with EVar a when builtins.Contains a && not (fnames.Contains a) -> shadowed.Add a |> ignore | _ -> ())
            match lvs, rhs with
            | [ EVar f ], [ EBlock _ as fn ] when modifier.IsSome -> SAssign([ EVar f ], [ wrap fn ])
            | _ when modifier.IsSome -> errAt modAt $"{modifier.Value} marks a function: {modifier.Value} f x = … or {modifier.Value} f = {{…}}"
            | [ EVar a ], [ EVar _ ] when stdAliases.ContainsKey a -> SExpr(EInt 0L)
            | _ -> SAssign(lvs, rhs)
        | Some op when op <> ":=" ->
            let lv = p.LValues().Head
            advance ()
            SCompound(op.Substring(0, op.Length - 1), lv, p.Expr())
        | _ ->
            let e = p.Expr()
            if accept "~{" then SWhile(e, p.Block "}") else SExpr e

    /// Is the token before pos inside a ternary's then-branch at this bracket depth: an earlier `?` of the
    /// statement that no `:` answers yet? A `:` right after a function or parameter name is a call, not an answer.
    member p.InThen() =
        let mutable k, d, pending, colons = pos - 2, 0, 0, 0
        while k >= 0 && tokens.[k].tok <> TERM && d >= 0 do
            match tokens.[k].tok with
            | SYM(")" | "]" | "}") -> d <- d + 1
            | SYM s when opensBracket s -> d <- d - 1
            | SYM "?" when d = 0 -> if colons > 0 then colons <- colons - 1 else pending <- pending + 1
            | SYM ":" when d = 0 && not (k > 0 && (match tokens.[k - 1].tok with ID g -> isFunc g || params.Contains g | _ -> false)) ->
                colons <- colons + 1
            | _ -> ()
            k <- k - 1
        pending > 0

    /// `name param {param} =` starts a function definition: Some(name, params, index of `=`).
    /// A param is a name or `(a, b, …)`, which unpacks a tuple or array argument (`_` skips an element).
    member p.FnDefAhead() =
        let name j = match tokens.[j].tok with ID n when assignable n -> Some n | _ -> None
        let rec group j acc =
            let name j = if tokens.[j].tok = ID "_" then Some "_" else name j // `_` skips an element
            match name j, tokens.[j + 1].tok with
            | Some n, SYM "," -> group (j + 2) (n :: acc)
            | Some n, SYM ")" -> Some(List.rev (n :: acc), j + 2)
            | _ -> None
        let param j =
            match name j with
            | Some n -> Some([ n ], j + 1)
            | None when tokens.[j].tok = SYM "(" -> group (j + 1) []
            | None -> None
        let rec pars j acc =
            match param j with
            | Some(ps, j) -> pars j (ps :: acc)
            | None -> List.rev acc, j
        match name pos with
        | Some f when not (foreign.ContainsKey f) ->
            match pars (pos + 1) [] with
            | ps, j when not ps.IsEmpty && tokens.[j].tok = SYM "=" -> Some(f, ps, j)
            | _ -> None
        | _ -> None

    /// `name pattern {pattern} =` with a literal somewhere: a base case of f (`f 0 (1, x) [a, b] = a + x`).
    /// A pattern is a name, `_`, a literal, `(p, …)` (a tuple) or `[p, …]` (an array of exactly that length).
    member p.ClauseAhead() : (string * Pat list * int) option =
        let rec pat j =
            match tokens.[j].tok with
            | ID n when n = "_" || assignable n -> Some(PName n, j + 1)
            | INT n -> Some(PLit(EInt n), j + 1)
            | FLOAT x -> Some(PLit(EFloat x), j + 1)
            | BOOL b -> Some(PLit(EBool b), j + 1)
            | STR [] -> Some(PLit(EStr ""), j + 1)
            | STR [ Lit s ] -> Some(PLit(EStr s), j + 1)
            | SYM "-" -> (match tokens.[j + 1].tok with INT n -> Some(PLit(EInt -n), j + 2) | FLOAT x -> Some(PLit(EFloat -x), j + 2) | _ -> None)
            | SYM("(" | "[" | "f[" as o) ->
                let close = if o = "(" then ")" else "]"
                let rec items j acc =
                    if tokens.[j].tok = SYM close && (o <> "(" || not (List.isEmpty acc)) then Some(PGroup(o <> "(", List.rev acc), j + 1) else
                    match pat j with
                    | Some(x, k) when tokens.[k].tok = SYM "," -> items (k + 1) (x :: acc)
                    | Some(x, k) when tokens.[k].tok = SYM close -> items k (x :: acc)
                    | _ -> None
                items (j + 1) []
            | _ -> None
        let rec lit x = match x with PLit _ -> true | PName _ -> false | PGroup(arr, xs) -> arr || List.exists lit xs
        let rec pats j acc =
            match pat j with
            | Some(x, k) -> pats k (x :: acc)
            | None -> List.rev acc, j
        match tokens.[pos].tok with
        | ID f when assignable f && not (foreign.ContainsKey f) && not (builtins.Contains f) ->
            match pats (pos + 1) [] with
            | ps, j when List.exists lit ps && tokens.[j].tok = SYM "=" -> Some(f, ps, j)
            | _ -> None
        | _ -> None

    /// A base case becomes a hidden function `f'k` of its pattern's names, and a guard `cond ? ret: f'k: names`
    /// that f's general definition puts first in its body.
    member p.Clause(f, pats: Pat list, eq, modifier: string option) =
        let l = line ()
        pos <- eq + 1
        let conds, binds = ResizeArray(), ResizeArray()
        let rec walk v x =
            match x with
            | PName "_" -> ()
            | PName n -> binds.Add(n, v)
            | PLit e -> conds.Add(EBin("=", v, e))
            | PGroup(arr, xs) ->
                if arr then conds.Add(EBin("=", EUn("#", v), EInt(int64 xs.Length)))
                xs |> List.iteri (fun i x -> walk (EIndex(v, EInt(int64 i))) x)
        pats |> List.iteri (fun i x -> walk (EVar $"_{i + 1}") x)
        let names = binds |> Seq.map fst |> List.ofSeq
        if List.distinct names <> names then errAt eq $"a name appears twice in a base case of '{f}'"
        let saved, savedParams = HashSet shadowed, HashSet params
        for n in names do
            if builtins.Contains n then shadowed.Add n |> ignore
            params.Add n |> ignore
        let body = if accept "{" then p.Block "}" else [ SLine(line ()); SExpr(p.Expr()) ]
        shadowed.Clear(); shadowed.UnionWith saved
        params.Clear(); params.UnionWith savedParams
        // a later condition indexes into what an earlier one checked the length of: `c1 ? c2 : F`, not `c1 & c2`
        let cond = Seq.reduceBack (fun c rest -> ETern(c, rest, EBool false)) conds
        nextId.Value <- nextId.Value + 1
        let id = nextId.Value
        let value, def =
            if names.IsEmpty then EGroup body, SExpr(EInt 0L)
            else
                let h = $"{f}'{id}"
                fnames.Add h |> ignore
                ECall(callMark + h, List.ofSeq (Seq.map snd binds)), SAssign([ EVar h ], [ EBlock(id, names |> List.map (fun n -> [ n ]), body) ])
        let guard = SExpr(ETern(cond, EGroup [ SExpr(ECall(callMark + "ret", [ value ])); SExpr(EInt 0L) ], EInt 0L))
        let n, gs, m = match clauses.TryGetValue f with | true, c -> c | _ -> pats.Length, [], None
        if n <> pats.Length then errAt eq $"base cases of '{f}' have different numbers of parameters"
        clauses.[f] <- (n, gs @ [ SLine l; guard ], (if m.IsSome then m else modifier))
        def

    /// Does `lvalue {, lvalue} =` (or `lvalue op=`) start here? Walks the tokens LValues would take; Some the operator.
    member p.AssignAhead() : string option =
        let tok j = tokens.[j].tok
        // past one token, or a whole bracket group
        let skip j =
            match tok j with
            | SYM s when opensBracket s ->
                let mutable j, depth = j + 1, 1
                while depth > 0 do
                    match tok j with
                    | SYM s when opensBracket s -> depth <- depth + 1
                    | SYM(")" | "]" | "}") -> depth <- depth - 1
                    | _ -> ()
                    j <- j + 1
                j
            | _ -> j + 1
        // an index operand: prefixes, a Base (a `f:` call there swallows any `=`), maybe `..` and a range end
        let rec atom j =
            match tok j with
            | SYM("-" | "#") -> atom (j + 1)
            | ID _ when tok (j + 1) = SYM ":" -> None
            | _ -> Some(skip j)
        let rec rangeEnd j =
            atom j |> Option.bind (fun j -> match tok j with SYM("+" | "-" | "*" | "**" | "/" | "%") -> rangeEnd (j + 1) | _ -> Some j)
        let operand j = atom j |> Option.bind (fun j -> if tok j = SYM ".." then rangeEnd (j + 1) else Some j)
        let rec indices j = if tok j = SYM "@" || tok j = SYM "@%" then operand (j + 1) |> Option.bind indices else Some j
        let rec lvs j n =
            match tok j with
            | ID name when assignable name ->
                indices (j + 1) |> Option.bind (fun j -> if tok j = SYM "," then lvs (j + 1) (n + 1) else Some(j, n))
            | _ -> None
        match lvs pos 1 with
        | _ when tok pos = SYM "(" ->
            // `(a,b) = t`: a parenthesised destructuring
            match lvs (pos + 1) 1 with
            | Some(j, n) when n > 1 && tok j = SYM ")" && tok (j + 1) = SYM "=" -> Some "="
            | _ -> None
        | Some(j, _) when tok j = SYM "=" -> Some "="
        | Some(j, 1) -> (match tok j with SYM op when op = ":=" || compounds.Contains op -> Some op | _ -> None)
        | _ -> None

    member p.LValues() =
        let one () =
            match peek () with
            // any name may be (re)defined, so user functions can shadow built-ins; implicit names never
            | ID name when assignable name ->
                let mutable e = mark pos (EVar name)
                advance ()
                let mutable go = true
                while go do
                    if accept "@" then e <- EIndex(e, p.IndexOperand())
                    elif accept "@%" then e <- wrapIndex e (p.IndexOperand())
                    else go <- false
                e
            | t -> err ($"cannot assign to {describeTok t}")
        let paren = accept "("
        let rest = ResizeArray [ one () ]
        while accept "," do rest.Add(one ())
        if paren then expect ")"
        List.ofSeq rest

    member p.ExprList() =
        let items = ResizeArray [ p.Expr() ]
        while accept "," do items.Add(p.Expr())
        List.ofSeq items

    member p.Expr() : Expr =
        // `lv := e` assigns inside an expression (a ternary branch, a filter); its value is lv's new value
        if p.AssignAhead() = Some ":=" then
            let lv = p.LValues().Head
            advance ()
            let e = p.Expr()
            EGroup [ SAssign([ lv ], [ e ]); SExpr lv ]
        else
        let c = p.Coalesce()
        let j = pos
        if accept "?" then
            let a = p.Expr()
            mark j (if accept ":" then ETern(c, a, p.Expr()) else onlyIf c a)
        else
            match peek () with
            | SYM s when Parser.SuffixHooks.ContainsKey s -> (advance (); Parser.SuffixHooks.[s] p c)
            | _ -> c

    /// One left-associative level: `next {op next}` for op in ops.
    member p.LeftAssoc(ops: string list, next: unit -> Expr) =
        let mutable e = next ()
        let mutable op = List.tryFind isSym ops
        while op.IsSome do
            let j = pos
            advance ()
            e <- mark j (EBin(op.Value, e, next ()))
            op <- List.tryFind isSym ops
        e

    /// `m ?? d`: gives map m the default value d for missing keys.
    member p.Coalesce() : Expr =
        let l = p.Or()
        let j = pos
        if accept "??" then mark j (EBin("??", l, p.Or())) else l

    member p.Or() = p.LeftAssoc([ "|" ], p.Xor)
    member p.Xor() = p.LeftAssoc([ "^" ], p.And)
    member p.And() = p.LeftAssoc([ "&" ], p.Not)

    /// `!f` of a function is the negated function `{!f:_}`; `!{..}` negates the block's value.
    member p.Not() =
        let j = pos
        if not (accept "!") then p.Compare()
        else
            let l = line ()
            match p.Not() with
            | EVar f when isFunc f ->
                nextId.Value <- nextId.Value + 1
                ECall(negMark + f, [ EBlock(nextId.Value, [], [ SLine l; SExpr(EUn("!", markCall nameMark f [ EVar "_" ])) ]) ])
            | EBlock(id, ps, body) when (match List.tryLast body with Some(SExpr _) -> true | _ -> false) ->
                // "!b": the block must return a bool (no truthiness), checked by the typer
                let (SExpr r) = List.last body in EBlock(id, ps, List.take (body.Length - 1) body @ [ SExpr(EUn("!b", r)) ])
            | x -> mark j (EUn("!", x))

    member p.Compare() =
        let l = p.Shift()
        match peek () with
        | SYM ("=" | "!=" | "<" | ">" | "<=" | ">=" as op) ->
            // a section `{=2}` (the lexer inserts `_` at the operator's position) compares; see `compared`
            let section = l = EVar "_" && tokens.[pos - 1].line = tokens.[pos].line && tokens.[pos - 1].col = tokens.[pos].col
            let j = pos
            advance ()
            let e = mark j (EBin(op, l, p.Shift()))
            let e = if section then compared e else e
            match peek () with
            | SYM ("=" | "!=" | "<" | ">" | "<=" | ">=" as op2) -> err ($"comparisons don't chain: write a {op} b & b {op2} c, or (a {op} b) {op2} c")
            | _ -> e
        | ID "in" -> (advance (); EHas(p.Shift(), l)) // `x in xs` is `xs @? x`
        | _ -> l

    member p.Shift() = p.LeftAssoc([ "<<"; ">>"; ">>>" ], p.Add)
    member p.Add() = p.LeftAssoc([ "+"; "-"; "++" ], p.Mul)
    member p.Mul() = p.LeftAssoc([ "*"; "**"; "/"; "%" ], p.Pow)

    member p.Pow() =
        let b = p.Prefix()
        let j = pos
        if accept "^^" then mark j (EBin("^^", b, p.Pow())) else b

    member p.Prefix() : Expr =
        let j = pos
        if accept "-" then mark j (EUn("-", p.Prefix()))
        elif accept "#" then mark j (EUn("#", p.Prefix()))
        else p.Postfix()

    member p.Postfix() =
        let mutable e = p.Primary()
        let mutable go = true
        while go do
            let j = if isSym "." then pos + 1 else pos // a method call is at its name
            match peek () with
            | SYM "m{" -> (advance (); e <- EIter(IMap, e, p.MapSection(p.Block "}")))
            | SYM "f[" -> (advance (); e <- EIter(IFilter, e, predicate (p.SectionBlock "]")))
            | SYM s when Parser.PostfixHooks.ContainsKey s -> (advance (); e <- Parser.PostfixHooks.[s] p e)
            | SYM "@" -> (advance (); e <- EIndex(e, p.IndexOperand()))
            | SYM "@?" -> (advance (); e <- EHas(e, p.IndexOperand()))
            | SYM "@%" ->
                advance ()
                let i = p.IndexOperand()
                match e with
                | EVar _ -> e <- wrapIndex e i
                | _ ->
                    // evaluate x once: (t = x; t@(i % #t))
                    nextId.Value <- nextId.Value + 1
                    let t = EVar $"wrap%%{nextId.Value}"
                    e <- EGroup [ SAssign([ t ], [ e ]); SExpr(wrapIndex t i) ]
            | SYM "." ->
                advance ()
                match peek () with
                | ID f when f.Length = 2 && f.[0] = '_' && f.[1] >= '1' && f.[1] <= '9' ->
                    advance ()
                    e <- EIndex(e, EInt(int64 f.[1] - int64 '1')) // `t._1` is `t@0`
                | ID f ->
                    advance ()
                    let args = if accept ":" then p.ExprList() else []
                    e <- ECall((match stdAliases.TryGetValue f with | true, b -> b | _ -> f), e :: args)
                | t -> err ($"expected a function name after '.', found {describeTok t}")
            | _ -> go <- false
            if go then mark j e |> ignore
        e

    /// `x@i@j` is `(x@i)@j`: an index is a base with optional prefix - or #, maybe a slice range
    /// (`x@-k..x` starts at -k). Slices may be open: `x@..n` from the start, `x@i..` to the end.
    member p.IndexOperand() : Expr =
        let rec start () =
            if accept "-" then EUn("-", start ())
            elif accept "#" then EUn("#", start ())
            else p.Base()
        if accept ".." then ERange(EInt 0L, p.RangeEnd())
        else
            let i = start ()
            if not (accept "..") then i
            else
                match peek () with
                | INT _ | ID _ | SYM("(" | "-" | "#") -> ERange(i, p.RangeEnd())
                | _ -> ERange(i, EInt System.Int64.MaxValue) // slices clamp to the length

    member p.Primary() =
        let e = p.Base()
        if not (accept "..") then e
        else
            let r = ERange(e, p.RangeEnd())
            if isSym ".." then err "ranges don't chain and have no step: for 0,2,4,… write 0..5{_*2} or 0..10[_%2=0]"
            r

    /// `a..n-1` ends at n-1: the end is + - * / % over atoms, so postfix forms (`{}` `[]` `/+`) still apply to the range.
    member p.RangeEnd() = p.LeftAssoc([ "+"; "-" ], fun () -> p.LeftAssoc([ "*"; "**"; "/"; "%" ], p.Atom))

    /// Range operands: a literal, a name or a parenthesised expression, optionally with prefix - or #.
    member p.Atom() =
        if accept "-" then EUn("-", p.Atom())
        elif accept "#" then EUn("#", p.Atom())
        else
            match peek () with
            | INT _ | ID _ | SYM "(" -> p.Base()
            | t -> err ($"a range end must be a number, a name or (expr), found {describeTok t}")

    member p.Base() : Expr = let at = pos in mark at (p.BaseAt at)

    member p.BaseAt(at: int) : Expr =
        let t = peek ()
        advance ()
        let key = match t with SYM s -> s | STR parts when parts |> List.exists (function Code _ -> true | _ -> false) -> "STR{" | _ -> ""
        match Parser.PrefixHooks.TryGetValue key with
        | true, hook -> hook p at
        | _ ->
        match t with
        | INT v -> EInt v
        | FLOAT v -> EFloat v
        | BOOL v -> EBool v
        | STR [ Lit s ] -> EStr s
        | STR parts -> EStr(parts |> List.map (function Lit s -> s | Code _ -> "") |> String.concat "")
        | ID name when isFunc name && isSym ":" -> (advance (); markCall callMark name (p.ExprList()))
        // a parameter may be a block passed in (`apply f x = f: x`, `{_1: _2}`), except where the `:` ends the
        // then-branch of a ternary (`c ? a : b`, `_ > 3 ? _ : 0`)
        | ID name when isSym ":" && (params.Contains name || (name.StartsWith "_" && Seq.forall System.Char.IsAsciiDigit (name.Substring 1)))
                       && not (p.InThen()) ->
            advance ()
            ECall(callMark + name, p.ExprList())
        | ID name when zeroArity.Contains name -> ECall(nameMark + name, [])
        | ID name -> EVar name
        | SYM "(" when p.GroupAhead() -> EGroup(p.Block ")")
        | SYM "(" ->
            let e = p.Expr()
            if accept "," then
                let items = ResizeArray [ e ]
                items.Add(p.Expr())
                while accept "," do items.Add(p.Expr())
                expect ")"
                ETuple(List.ofSeq items)
            else
                expect ")"
                compared e
        | SYM "[" ->
            if accept "->" then (expect "]"; EMapLit [])
            elif accept "]" then EArr []
            else
                let first = p.Expr()
                if accept "->" then
                    let pairs = ResizeArray [ first, p.Expr() ]
                    while accept "," do
                        let k = p.Expr()
                        expect "->"
                        pairs.Add(k, p.Expr())
                    expect "]"
                    EMapLit(List.ofSeq pairs)
                else
                    let items = ResizeArray [ first ]
                    while accept "," do items.Add(p.Expr())
                    expect "]"
                    EArr(List.ofSeq items)
        | SYM "{" ->
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            EBlock(id, [], p.SectionBlock "}")
        | SYM "=" when at > 0 && tokens.[at - 1].tok = SYM "=" -> errAt at "'==' is not an operator: '=' compares"
        | SYM "=" when at > 0 && tokens.[at - 1].tok = SYM "!=" -> errAt at "'!==' is not an operator: '!=' compares"
        | SYM ">" when at > 0 && tokens.[at - 1].tok = SYM "<" -> errAt (at - 1) "'<>' is not an operator: write != for not-equal"
        | SYM("&" | "|" as s) when at > 0 && tokens.[at - 1].tok = SYM s -> errAt (at - 1) $"'{s}{s}' is not an operator: write {s}"
        | t -> errAt at ($"expected an expression, found {describeTok t}")

    /// Does `target` occur at this bracket's top level before the statement (or the bracket) ends?
    member p.Ahead(target: Tok) =
        let mutable j, depth = pos, 0
        let mutable found, fin = false, false
        while not fin do
            match tokens.[j].tok with
            | t when depth = 0 && t = target -> (found <- true; fin <- true)
            | TERM when depth = 0 -> fin <- true
            | EOF -> fin <- true
            | SYM s when opensBracket s -> depth <- depth + 1
            | SYM(")" | "]" | "}") -> (depth <- depth - 1; if depth < 0 then fin <- true)
            | _ -> ()
            j <- j + 1
        found

    /// After `(`: a `;` at this paren's top level makes it a statement group `(s1; s2; value)`.
    member p.GroupAhead() = p.Ahead TERM

/// Parses a file's tokens; `path` anchors relative imports.
let parse (tokens: Token list) (path: string) : Stmt list =
    let path = System.IO.Path.GetFullPath path
    let imports = { dir = System.IO.Path.GetDirectoryName path; seen = HashSet [ path ] }
    imported.Value.Clear()
    Parser(Array.ofList tokens, HashSet<string>(), HashSet<string>(), HashSet<string>(), Dictionary(), ref 0, imports).Program()
