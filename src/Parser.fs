/// Stage 2: tokens -> surface AST. Recursive descent, one function per precedence level.
module Parser

open System.Collections.Generic
open Ast

/// language primitives plus every function declared in runtime.c (see Stdlib.fs)
let builtins = set [ "print"; "println"; "int"; "float"; "str"; "bool"; "pop"; "keys"; "vals"; "arr"; "set"; "Q"; "PQ" ] + Stdlib.names
let private zeroArity = Stdlib.zeroArity
let private compounds = set [ "+="; "-="; "*="; "/="; "%="; "^^="; "++="; "&="; "|="; "^="; "<<="; ">>="; ">>>=" ]

/// Keywords of other languages: a statement starting with one gets a hint.
let private foreign =
    let fn = "define a function by assigning a block: f = {_*2}"
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
    | SYM("(" | "[" | "{" | "%[" | "Q[" | "PQ[" | "G[" | "DG[") -> true
    | _ -> false

/// Directory of the file being parsed, and every file already imported (each is included once).
type private Imports = { dir: string; seen: HashSet<string> }

type private Parser(tokens: Token[], fnames: HashSet<string>, nextId: int ref, imports: Imports) =
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

    member p.Program() =
        let body = p.Stmts EOF
        if peek () <> EOF then err ($"unexpected {describeTok (peek ())}")
        body

    member p.Stmts(closer: Tok) : Stmt list =
        let out = ResizeArray<Stmt>()
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
                        let toks = Lexer.lex (System.IO.File.ReadAllText path) 1 1
                        let sub = Parser(Array.ofList toks, fnames, nextId, { imports with dir = System.IO.Path.GetDirectoryName path })
                        out.AddRange(sub.Program())
                | t -> err ($"import expects a file name string, found {describeTok t}")
            else
                let start = peek ()
                out.Add(SLine(line ()))
                out.Add(p.Stmt())
                if peek () = TERM then advance ()
                elif peek () <> closer then
                    match start with
                    | ID kw when foreign.ContainsKey kw -> err ($"unexpected {describeTok (peek ())} after '{kw}'; {foreign.[kw]}")
                    | _ -> err ($"expected end of statement, found {found ()}")
        List.ofSeq out

    member p.Block(closer: string) =
        let body = p.Stmts(SYM closer)
        expect closer
        body

    /// Function section: a body that is just a function name calls it on `_`, so `x{int}` is `x{int:_}`.
    member p.SectionBlock(closer: string) =
        match p.Block closer with
        | [ SLine l; SExpr(EVar f) ] when isFunc f -> [ SLine l; SExpr(ECall(f, [ EVar "_" ])) ]
        | body -> body

    member p.Stmt() : Stmt =
        let save = pos
        let lvs = try Some(p.LValues()) with CompileError _ -> None
        match lvs, peek () with
        | Some lvs, SYM "=" ->
            advance ()
            match lvs with
            | [ EVar f ] when isSym "{" -> fnames.Add f |> ignore
            | _ -> ()
            SAssign(lvs, p.ExprList())
        | Some [ lv ], SYM op when compounds.Contains op ->
            advance ()
            SCompound(op.Substring(0, op.Length - 1), lv, p.Expr())
        | _ ->
            pos <- save
            let e = p.Expr()
            if accept "~{" then SWhile(e, p.Block "}") else SExpr e

    member p.LValues() =
        let one () =
            match peek () with
            // implicit names (_ _1 @ $) are never assigned, so `{_=p}` / `[=p]` compare
            // any name may be (re)defined, so user functions can shadow built-ins; implicit names never
            | ID name when not (name.StartsWith "_" || name = "@" || name = "$") ->
                advance ()
                let mutable e = EVar name
                while accept "@" do e <- EIndex(e, p.IndexOperand())
                e
            | t -> err ($"cannot assign to {describeTok t}")
        let first = one ()
        let rest = ResizeArray [ first ]
        while accept "," do rest.Add(one ())
        List.ofSeq rest

    member p.ExprList() =
        let items = ResizeArray [ p.Expr() ]
        while accept "," do items.Add(p.Expr())
        List.ofSeq items

    member p.Expr() : Expr =
        let c = p.Or()
        if accept "?" then
            let a = p.Expr()
            // `c ? a` without `:` runs a only when c holds; its value is 0
            if accept ":" then ETern(c, a, p.Expr()) else ETern(c, EGroup [ SExpr a; SExpr(EInt 0L) ], EInt 0L)
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
        // an array-literal pattern (`[1,2] => …`) matches an array subject as a whole instead of per element
        let mutable whole = false
        let pattern (v: Expr) = (match v with EArr _ -> whole <- true | _ -> ()); v
        let test () =
            match subject, peek () with
            | Some _, SYM("=" | "!=" | "<" | ">" | "<=" | ">=" as op) -> (advance (); EBin(op, EVar "_", pattern (p.Shift())))
            | Some _, _ -> EBin("=", EVar "_", pattern (p.Expr()))
            | None, _ -> p.Expr()
        let skipTerms () = while peek () = TERM do advance ()
        // an arm's result may be a statement (`5 => c += 1`); its value is then 0
        let result () = match p.Stmt() with SExpr e -> e | st -> EGroup [ st; SExpr(EInt 0L) ]
        let arms = ResizeArray<Expr * Expr>()
        let mutable deflt = if filter then Some(EBool false) else None
        let mutable hasDefault = false
        skipTerms ()
        while not (isSym closer) do
            if hasDefault then err "the default arm (`_ => …`, or one without '=>') must be the last arm"
            if peek () = ID "_" && tokens.[pos + 1].tok = SYM "=>" then
                advance (); advance ()
                deflt <- Some(result ())
                hasDefault <- true
            else
                let start = pos
                let tests = ResizeArray [ test () ]
                while accept "," do tests.Add(test ())
                let cond = tests |> Seq.reduce (fun a b -> EBin("|", a, b))
                if accept "=>" then arms.Add(cond, result ())
                elif filter then arms.Add(cond, EBool true)
                else
                    pos <- start
                    deflt <- Some(result ())
                    hasDefault <- true
            if peek () = TERM then skipTerms ()
            elif not (isSym closer) then err ($"expected '=>', a new arm or '{closer}', found {found ()}")
        expect closer
        let chain =
            Seq.foldBack (fun (c, r) acc ->
                Some(match acc with
                     | Some e -> ETern(c, r, e)
                     | None -> ETern(c, EGroup [ SExpr r; SExpr(EInt 0L) ], EInt 0L))) arms deflt
        match chain, subject with
        | _ when arms.Count = 0 && deflt.IsNone || filter && arms.Count = 0 -> err "a match needs at least one arm"
        | Some e, Some s -> EMatch((if filter then "filter" elif whole then "whole" else "map"), s, e)
        | e, _ -> e.Value

    member p.Or() =
        let mutable e = p.Xor()
        while accept "|" do e <- EBin("|", e, p.Xor())
        e

    member p.Xor() =
        let mutable e = p.And()
        while accept "^" do e <- EBin("^", e, p.And())
        e

    member p.And() =
        let mutable e = p.Not()
        while accept "&" do e <- EBin("&", e, p.Not())
        e

    member p.Not() = if accept "!" then EUn("!", p.Not()) else p.Compare()

    member p.Compare() =
        let l = p.Shift()
        match peek () with
        | SYM ("=" | "!=" | "<" | ">" | "<=" | ">=" as op) -> (advance (); EBin(op, l, p.Shift()))
        | _ -> l

    member p.Shift() =
        let mutable e = p.Add()
        let mutable go = true
        while go do
            match peek () with
            | SYM ("<<" | ">>" | ">>>" as op) -> (advance (); e <- EBin(op, e, p.Add()))
            | _ -> go <- false
        e

    member p.Add() =
        let mutable e = p.Mul()
        let mutable go = true
        while go do
            match peek () with
            | SYM ("+" | "-" | "++" as op) -> (advance (); e <- EBin(op, e, p.Mul()))
            | _ -> go <- false
        e

    member p.Mul() =
        let mutable e = p.Pow()
        let mutable go = true
        while go do
            match peek () with
            | SYM ("*" | "/" | "%" as op) -> (advance (); e <- EBin(op, e, p.Pow()))
            | _ -> go <- false
        e

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
            | SYM "m{" -> (advance (); e <- EIter("map", e, p.SectionBlock "}"))
            | SYM "f[" -> (advance (); e <- EIter("filter", e, p.SectionBlock "]"))
            | SYM "/{" -> (advance (); e <- EIter("fold", e, p.Block "}"))
            | SYM "\\{" -> (advance (); e <- EIter("scan", e, p.Block "}"))
            | FOLDOP op -> (advance (); e <- EIterOp("fold", e, op))
            | SCANOP op -> (advance (); e <- EIterOp("scan", e, op))
            | SYM "@" -> (advance (); e <- EIndex(e, p.IndexOperand()))
            | SYM "@?" -> (advance (); e <- EHas(e, p.IndexOperand()))
            | SYM "." ->
                advance ()
                match peek () with
                | ID f ->
                    advance ()
                    let args = if accept ":" then p.ExprList() else []
                    e <- ECall(f, e :: args)
                | t -> err ($"expected a function name after '.', found {describeTok t}")
            | _ -> go <- false
        e

    /// `x@i@j` is `(x@i)@j`: an index is a primary (maybe a range) with optional prefix - or #.
    member p.IndexOperand() : Expr =
        if accept "-" then EUn("-", p.IndexOperand())
        elif accept "#" then EUn("#", p.IndexOperand())
        else p.Primary()

    member p.Primary() =
        let e = p.Base()
        if accept ".." then ERange(e, p.RangeEnd()) else e

    /// `a..n-1` ends at n-1: the end is + - * / % over atoms, so postfix forms (`{}` `[]` `/+`) still apply to the range.
    member p.RangeEnd() =
        let term () =
            let mutable e = p.Atom()
            let mutable go = true
            while go do
                match peek () with
                | SYM("*" | "/" | "%" as op) -> (advance (); e <- EBin(op, e, p.Atom()))
                | _ -> go <- false
            e
        let mutable e = term ()
        let mutable go = true
        while go do
            match peek () with
            | SYM("+" | "-" as op) -> (advance (); e <- EBin(op, e, term ()))
            | _ -> go <- false
        e

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
                    let sub = Parser(Array.ofList (Lexer.lex src l c), fnames, nextId, imports)
                    sub.InterpExpr())
            |> EInterp
        | ID name when isFunc name && isSym ":" -> (advance (); ECall(name, p.ExprList()))
        | ID name when zeroArity.Contains name -> ECall(name, [])
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
                e
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
        | SYM("%[" | "Q[" | "PQ[" | "G[" | "DG[" as s) ->
            let items = if isSym "]" then [] else p.ExprList()
            expect "]"
            ECollLit((if s = "%[" then "set" else s.TrimEnd '['), items)
        | SYM "?{" -> p.Match(None, false)
        | SYM "{" ->
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            EBlock(id, p.SectionBlock "}")
        | SYM "=" when at > 0 && tokens.[at - 1].tok = SYM "=" -> errAt at "'==' is not an operator: '=' compares"
        | t -> errAt at ($"expected an expression, found {describeTok t}")

    /// After `(`: a `;` at this paren's top level makes it a statement group `(s1; s2; value)`.
    member p.GroupAhead() =
        let mutable j = pos
        let mutable depth = 0
        let mutable found = false
        while depth >= 0 && not found && tokens.[j].tok <> EOF do
            match tokens.[j].tok with
            | SYM s when s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{" -> depth <- depth + 1
            | SYM(")" | "]" | "}") -> depth <- depth - 1
            | TERM when depth = 0 -> found <- true
            | _ -> ()
            j <- j + 1
        found

    member p.InterpExpr() =
        let e = p.Expr()
        while peek () = TERM do advance ()
        if peek () <> EOF then err ($"expected '}}' to end the interpolation, found {describeTok (peek ())}")
        e

/// Parses a file's tokens; `path` anchors relative imports.
let parse (tokens: Token list) (path: string) : Stmt list =
    let path = System.IO.Path.GetFullPath path
    let imports = { dir = System.IO.Path.GetDirectoryName path; seen = HashSet [ path ] }
    Parser(Array.ofList tokens, HashSet<string>(), ref 0, imports).Program()
