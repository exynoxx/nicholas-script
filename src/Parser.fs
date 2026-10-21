/// Stage 2: tokens -> surface AST. Recursive descent, one function per precedence level.
module Parser

open System.Collections.Generic
open Ast

/// language primitives plus every function declared in runtime.c (see Stdlib.fs)
let builtins = set [ "print"; "println"; "int"; "float"; "str"; "bool"; "set"; "Q"; "PQ"; "BT"; "memo" ] + Stdlib.names
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

/// Can this token start an expression? (then a missing operator or ',' is the likely mistake)
let private startsExpr (t: Tok) =
    match t with
    | INT _ | FLOAT _ | STR _ | BOOL _ | ID _ -> true
    | SYM("(" | "[" | "{" | "%[" | "Q[" | "PQ[" | "BT[" | "G[" | "DG[") -> true
    | _ -> false

/// `c ? a` without `:` runs a only when c holds; its value is 0
let private onlyIf c a = ETern(c, EGroup [ SExpr a; SExpr(EInt 0L) ], EInt 0L)

/// Implicit names (_ _1 @ $) are never assigned, so `{_=p}` / `[=p]` compare.
let private assignable (name: string) = not (name.StartsWith "_" || name = "@" || name = "$")

/// In a map block a bare top-level `_ = e` writes the value back (Lower.iter); a parenthesised `(_ = e)` or a
/// section `{=e}` is a comparison, so it is wrapped in a group, which that write-back does not match.
let private compared e = match e with EBin("=", EVar "_", _) -> EGroup [ SExpr e ] | e -> e

let private opensBracket (s: string) = s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{"

/// Directory of the file being parsed, and every file already imported (each is included once).
type private Imports = { dir: string; seen: HashSet<string> }

type private Parser(tokens: Token[], fnames: HashSet<string>, stdAliases: Dictionary<string, string>, nextId: int ref, imports: Imports) =
    let mutable pos = 0
    let peek () = tokens.[pos].tok
    let line () = tokens.[pos].line
    let advance () = pos <- pos + 1
    let isSym s = peek () = SYM s
    let accept s = if isSym s then (advance (); true) else false
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
        describeTok t + (if startsExpr t then " (missing an operator or ','?)" else "")
    let expect s = if not (accept s) then err ($"expected '{s}' but found {found ()}")
    let isFunc name = fnames.Contains name || builtins.Contains name
    let markCall (mark: string) f args =
        match stdAliases.TryGetValue f with
        | true, b -> ECall(b, args)
        | _ -> ECall(mark + f, args)
    let aliasStd a f =
        if stdAliases.ContainsKey f || (builtins.Contains f && not (fnames.Contains f) && not (zeroArity.Contains f)) then
            stdAliases.[a] <- (match stdAliases.TryGetValue f with | true, b -> b | _ -> f)
            fnames.Add a |> ignore

    member p.Program() =
        p.DeclareFunctions()
        let body = p.Stmts EOF
        if peek () <> EOF then err ($"unexpected {describeTok (peek ())}")
        body

    /// Forward references: every top-level `f a b = …`, `f = {…}` and `f = memo:…` is a function name from the start,
    /// so functions can call ones defined later (mutual recursion).
    member p.DeclareFunctions() =
        let mutable depth = 0
        let mutable stmtStart = true
        for j in 0 .. tokens.Length - 1 do
            if stmtStart && depth = 0 then
                pos <- j
                match p.FnDefAhead(), tokens.[j].tok with
                | Some(f, _, _), _ -> fnames.Add f |> ignore
                | None, ID f when j + 2 < tokens.Length && tokens.[j + 1].tok = SYM "="
                                  && (tokens.[j + 2].tok = SYM "{" || tokens.[j + 2].tok = ID "memo") -> fnames.Add f |> ignore
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
                        let sub = Parser(Array.ofList toks, fnames, stdAliases, nextId, { imports with dir = System.IO.Path.GetDirectoryName path })
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
                    | _ when isSym ":" && (match tokens.[pos - 1].tok with ID _ -> true | _ -> false) ->
                        errAt (pos - 1) ($"{describeTok tokens.[pos - 1].tok} is not a function: define it with a block, e.g. f = {{_+1}}")
                    | _ -> err ($"expected end of statement, found {found ()}")
        if closer = EOF then noop ()
        List.ofSeq out

    member p.Block(closer: string) =
        let body = p.Stmts(SYM closer)
        expect closer
        body

    /// Function section: a body that is just a function name calls it on `_`, so `x{int}` is `x{int:_}`; `x[!ws]` is `x[!ws:_]`;
    /// `x[!{..}]` binds the negated block to a hidden name and calls it on `_`.
    member p.SectionBlock(closer: string) =
        match p.Block closer with
        | [ SLine l; SExpr(EVar f) ] when isFunc f -> [ SLine l; SExpr(markCall nameMark f [ EVar "_" ]) ]
        | [ SLine l; SExpr(EBlock(id, [], body) as b) ] when (match List.tryLast body with Some(SExpr(EUn("!b", _))) -> true | _ -> false) ->
            let f = $"not'{id}"
            [ SLine l; SAssign([ EVar f ], [ b ]); SExpr(ECall(callMark + f, [ EVar "_" ])) ]
        | [ SLine _; SExpr(ECall(f, [ EBlock(_, [], body) ])) ] when f.StartsWith negMark -> body
        | body -> body

    member p.Stmt() : Stmt =
        match p.FnDefAhead() with
        | Some(f, ps, eq) ->
            // `f a b = body` is `f = {body}` with a, b naming _1, _2; a `{…}` body holds statements
            pos <- eq + 1
            fnames.Add f |> ignore
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            let body = if accept "{" then p.Block "}" else [ SLine(line ()); SExpr(p.Expr()) ]
            SAssign([ EVar f ], [ EBlock(id, ps, body) ])
        | None ->
        match p.AssignAhead() with
        | Some "=" ->
            let lvs = p.LValues()
            advance ()
            match lvs with
            | [ EVar f ] when isSym "{" || (peek () = ID "memo" && tokens.[pos + 1].tok = SYM ":") -> fnames.Add f |> ignore
            | _ -> ()
            let rhs = p.ExprList()
            match lvs, rhs with
            | [ EVar f ], [ ECall(g, _) ] when g.StartsWith negMark -> fnames.Add f |> ignore // g = !ws
            | [ EVar f ], [ EBlock _ ] -> fnames.Add f |> ignore // g = !{..}
            | [ EVar a ], [ EVar f ] when stdAliases.ContainsKey f || (builtins.Contains f && not (fnames.Contains f)) -> aliasStd a f
            | [ EVar a ], [ EVar f ] when fnames.Contains f -> fnames.Add a |> ignore
            | _ -> ()
            match lvs, rhs with
            | [ EVar a ], [ EVar _ ] when stdAliases.ContainsKey a -> SExpr(EInt 0L)
            | _ -> SAssign(lvs, rhs)
        | Some op ->
            let lv = p.LValues().Head
            advance ()
            SCompound(op.Substring(0, op.Length - 1), lv, p.Expr())
        | None ->
            let e = p.Expr()
            if accept "~{" then SWhile(e, p.Block "}") else SExpr e

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
        let rec indices j = if tok j = SYM "@" then operand (j + 1) |> Option.bind indices else Some j
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
        | Some(j, 1) -> (match tok j with SYM op when compounds.Contains op -> Some op | _ -> None)
        | _ -> None

    member p.LValues() =
        let one () =
            match peek () with
            // any name may be (re)defined, so user functions can shadow built-ins; implicit names never
            | ID name when assignable name ->
                advance ()
                let mutable e = EVar name
                while accept "@" do e <- EIndex(e, p.IndexOperand())
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
        let c = p.Coalesce()
        if accept "?" then
            let a = p.Expr()
            if accept ":" then ETern(c, a, p.Expr()) else onlyIf c a
        elif accept "?{" then p.Match(Some c, false)
        elif accept "?[" then p.Match(Some c, true)
        else c

    /// `x ?{ 1 => a; 2, 3 => b; >9 => c; d }` compares x with each arm's values (an arm starting with a comparison
    /// tests x with it; `_` is x); `?{ c1 => a; c2 => b; d }` takes the first true condition. The first matching arm
    /// wins; `_ => d` (or a last arm without `=>`) is the default; without one it is a ternary chain like `c ? a` (value 0).
    /// An array x maps the match over its elements, unless a pattern is an array literal (`[1,2] => …`): then x is
    /// matched whole. `xs ?[ 1, 2; >9 ]` keeps the elements matching an arm.
    member p.Match(subject: Expr option, filter: bool) : Expr =
        let closer = if filter then "]" else "}"
        let test () =
            match subject, peek () with
            | Some _, SYM("=" | "!=" | "<" | ">" | "<=" | ">=" as op) -> (advance (); EBin(op, EVar "_", p.Shift()))
            | Some _, _ -> EBin("=", EVar "_", p.Expr())
            | None, _ -> p.Expr()
        let skipTerms () = while peek () = TERM do advance ()
        // an arm's result may be a statement (`5 => c += 1`); its value is then 0. `=> { s1; s2 }` runs several
        // statements in the match's scope (`_` stays the subject); the last expression is the value, else 0
        let result () =
            if isSym "{" then
                advance ()
                let body = p.Block "}"
                match body |> List.filter (function SLine _ -> false | _ -> true) |> List.tryLast with
                | Some(SExpr _) -> EGroup body
                | _ -> EGroup(body @ [ SExpr(EInt 0L) ])
            else
                match p.Stmt() with SExpr e -> e | st -> EGroup [ st; SExpr(EInt 0L) ]
        let arms = ResizeArray<Expr list * Expr>()
        let mutable deflt = None
        skipTerms ()
        while not (isSym closer) do
            if deflt.IsSome then err "the default arm (`_ => …`, or one without '=>') must be the last arm"
            if peek () = ID "_" && tokens.[pos + 1].tok = SYM "=>" then
                advance (); advance ()
                deflt <- Some(result ())
            elif filter || p.Ahead(SYM "=>") then
                let tests = ResizeArray [ test () ]
                while accept "," do tests.Add(test ())
                if accept "=>" then arms.Add(List.ofSeq tests, result ())
                elif filter then arms.Add(List.ofSeq tests, EBool true)
            else deflt <- Some(result ())
            if peek () = TERM then skipTerms ()
            elif not (isSym closer) then err ($"expected '=>', a new arm or '{closer}', found {found ()}")
        expect closer
        if arms.Count = 0 && (filter || deflt.IsNone) then err "a match needs at least one arm"
        let deflt = if filter && deflt.IsNone then Some(EBool false) else deflt
        let chain =
            Seq.foldBack (fun (tests, r) acc ->
                let c = List.reduce (fun a b -> EBin("|", a, b)) tests
                Some(match acc with Some e -> ETern(c, r, e) | None -> onlyIf c r)) arms deflt
        // an array-literal pattern (`[1,2] => …`) matches an array subject as a whole instead of per element
        let whole = arms |> Seq.exists (fst >> List.exists (function EBin(_, _, EArr _) -> true | _ -> false))
        match subject with
        | Some s -> EMatch((if filter then "filter" elif whole then "whole" else "map"), s, chain.Value)
        | None -> chain.Value

    /// One left-associative level: `next {op next}` for op in ops.
    member p.LeftAssoc(ops: string list, next: unit -> Expr) =
        let mutable e = next ()
        let mutable op = List.tryFind isSym ops
        while op.IsSome do
            advance ()
            e <- EBin(op.Value, e, next ())
            op <- List.tryFind isSym ops
        e

    /// `a ?? b`: b when a is null (a missing map key); right-associative.
    member p.Coalesce() : Expr =
        let l = p.Or()
        if accept "??" then EBin("??", l, p.Coalesce()) else l

    member p.Or() = p.LeftAssoc([ "|" ], p.Xor)
    member p.Xor() = p.LeftAssoc([ "^" ], p.And)
    member p.And() = p.LeftAssoc([ "&" ], p.Not)

    /// `!f` of a function is the negated function `{!f:_}`; `!{..}` negates the block's value.
    member p.Not() =
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
            | x -> EUn("!", x)

    member p.Compare() =
        let l = p.Shift()
        match peek () with
        | SYM ("=" | "!=" | "<" | ">" | "<=" | ">=" as op) ->
            // a section `{=2}` (the lexer inserts `_` at the operator's position) compares; see `compared`
            let section = l = EVar "_" && tokens.[pos - 1].line = tokens.[pos].line && tokens.[pos - 1].col = tokens.[pos].col
            advance ()
            let e = EBin(op, l, p.Shift())
            let e = if section then compared e else e
            match peek () with
            | SYM ("=" | "!=" | "<" | ">" | "<=" | ">=" as op2) -> err ($"comparisons don't chain: write a {op} b & b {op2} c, or (a {op} b) {op2} c")
            | _ -> e
        | _ -> l

    member p.Shift() = p.LeftAssoc([ "<<"; ">>"; ">>>" ], p.Add)
    member p.Add() = p.LeftAssoc([ "+"; "-"; "++" ], p.Mul)
    member p.Mul() = p.LeftAssoc([ "*"; "**"; "/"; "%" ], p.Pow)

    member p.Pow() =
        let b = p.Prefix()
        if accept "^^" then EBin("^^", b, p.Pow()) else b

    member p.Prefix() : Expr =
        if accept "-" then EUn("-", p.Prefix())
        elif accept "#" then EUn("#", p.Prefix())
        else p.Postfix()

    member p.Postfix() =
        let mutable e = p.Primary()
        let mutable go = true
        while go do
            match peek () with
            | SYM "m{" -> (advance (); e <- EIter(IMap, e, p.SectionBlock "}"))
            | SYM "f[" -> (advance (); e <- EIter(IFilter, e, p.SectionBlock "]"))
            | SYM "/{" -> (advance (); e <- EIter(IFold, e, p.Block "}"))
            | SYM "${" -> (advance (); e <- ESort(e, p.SectionBlock "}"))
            | SYM "\\{" -> (advance (); e <- EIter(IScan, e, p.Block "}"))
            | FOLDOP op -> (advance (); e <- EIterOp(IFold, e, op))
            | SCANOP op -> (advance (); e <- EIterOp(IScan, e, op))
            | SYM "@" -> (advance (); e <- EIndex(e, p.IndexOperand()))
            | SYM "@?" -> (advance (); e <- EHas(e, p.IndexOperand()))
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

    member p.Base() : Expr =
        let at = pos
        let t = peek ()
        advance ()
        match t with
        | INT v -> EInt v
        | FLOAT v -> EFloat v
        | BOOL v -> EBool v
        | STR [ Lit s ] -> EStr s
        | STR parts ->
            parts
            |> List.map (function
                | Lit s -> EStr s
                | Code(src, l, c) ->
                    let sub = Parser(Array.ofList (Lexer.lex src l c), fnames, stdAliases, nextId, imports)
                    sub.InterpExpr())
            |> EInterp
        | ID name when isFunc name && isSym ":" -> (advance (); markCall callMark name (p.ExprList()))
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
        | SYM("%[" | "Q[" | "PQ[" | "BT[" | "G[" | "DG[" as s) ->
            let items = if isSym "]" then [] else p.ExprList()
            expect "]"
            ECollLit((if s = "%[" then "set" else s.TrimEnd '['), items)
        | SYM "?{" -> p.Match(None, false)
        | SYM "{" ->
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            EBlock(id, [], p.SectionBlock "}")
        | SYM "=" when at > 0 && tokens.[at - 1].tok = SYM "=" -> errAt at "'==' is not an operator: '=' compares"
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

    member p.InterpExpr() =
        let e = p.Expr()
        while peek () = TERM do advance ()
        if peek () <> EOF then err ($"expected '}}' to end the interpolation, found {describeTok (peek ())}")
        e

/// Parses a file's tokens; `path` anchors relative imports.
let parse (tokens: Token list) (path: string) : Stmt list =
    let path = System.IO.Path.GetFullPath path
    let imports = { dir = System.IO.Path.GetDirectoryName path; seen = HashSet [ path ] }
    imported.Value.Clear()
    Parser(Array.ofList tokens, HashSet<string>(), Dictionary(), ref 0, imports).Program()
