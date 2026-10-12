/// Stage 2: tokens -> surface AST. Recursive descent, one function per precedence level.
module Parser

open System.Collections.Generic
open Ast

let builtins =
    set [ "print"; "read"; "lines"; "int"; "float"; "str"; "bool"; "sort"; "rev"; "split"; "join"; "ord"; "chr"
          "pop"; "keys"; "vals"; "arr"; "set"; "Q"; "PQ" ]
let private zeroArity = set [ "read"; "lines" ]
let private compounds = set [ "+="; "-="; "*="; "/="; "%="; "^^="; "++="; "&="; "|="; "^="; "<<="; ">>=" ]

/// Directory of the file being parsed, and every file already imported (each is included once).
type private Imports = { dir: string; seen: HashSet<string> }

type private Parser(tokens: Token[], fnames: HashSet<string>, nextId: int ref, imports: Imports) =
    let mutable pos = 0
    let peek () = tokens.[pos].tok
    let line () = tokens.[pos].line
    let advance () = pos <- pos + 1
    let isSym s = peek () = SYM s
    let accept s = if isSym s then (advance (); true) else false
    let expect s = if not (accept s) then fail (line ()) (sprintf "expected '%s' but found %A" s (peek ()))
    let isFunc name = fnames.Contains name || builtins.Contains name

    member p.Program() =
        let body = p.Stmts EOF
        if peek () <> EOF then fail (line ()) (sprintf "unexpected %A" (peek ()))
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
                    if not (System.IO.File.Exists path) then fail (line ()) (sprintf "import: file not found: %s" path)
                    if imports.seen.Add path then
                        let toks = Lexer.lex (System.IO.File.ReadAllText path) 1
                        let sub = Parser(Array.ofList toks, fnames, nextId, { imports with dir = System.IO.Path.GetDirectoryName path })
                        out.AddRange(sub.Program())
                | t -> fail (line ()) (sprintf "import expects a file name string, found %A" t)
            else
                out.Add(SLine(line ()))
                out.Add(p.Stmt())
                if peek () = TERM then advance ()
                elif peek () <> closer then fail (line ()) (sprintf "expected end of statement, found %A" (peek ()))
        List.ofSeq out

    member p.Block(closer: string) =
        let body = p.Stmts(SYM closer)
        expect closer
        body

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
            | ID name when not (isFunc name) && not (name.StartsWith "_" || name = "@" || name = "$") ->
                advance ()
                let mutable e = EVar name
                while accept "@" do e <- EIndex(e, p.IndexOperand())
                e
            | t -> fail (line ()) (sprintf "not an lvalue: %A" t)
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
            expect ":"
            ETern(c, a, p.Expr())
        else c

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

    member p.Not() = if accept "~" then EUn("~", p.Not()) else p.Compare()

    member p.Compare() =
        let l = p.Shift()
        match peek () with
        | SYM ("=" | "~=" | "<" | ">" | "<=" | ">=" as op) -> (advance (); EBin(op, l, p.Shift()))
        | _ -> l

    member p.Shift() =
        let mutable e = p.Add()
        let mutable go = true
        while go do
            match peek () with
            | SYM ("<<" | ">>" as op) -> (advance (); e <- EBin(op, e, p.Add()))
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
            | SYM "m{" -> (advance (); e <- EIter("map", e, p.Block "}"))
            | SYM "f[" -> (advance (); e <- EIter("filter", e, p.Block "]"))
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
                | t -> fail (line ()) (sprintf "expected method name, found %A" t)
            | _ -> go <- false
        e

    /// `x@i@j` is `(x@i)@j`: an index is a primary (maybe a range) with optional prefix - or #.
    member p.IndexOperand() : Expr =
        if accept "-" then EUn("-", p.IndexOperand())
        elif accept "#" then EUn("#", p.IndexOperand())
        else p.Primary()

    member p.Primary() =
        let e = p.Base()
        if accept ".." then ERange(e, p.Atom()) else e

    /// Range operands: a literal, a name or a parenthesised expression, optionally with prefix - or #.
    member p.Atom() =
        if accept "-" then EUn("-", p.Atom())
        elif accept "#" then EUn("#", p.Atom())
        else
            match peek () with
            | INT _ | ID _ | SYM "(" -> p.Base()
            | t -> fail (line ()) (sprintf "range end must be a number, name or (expr), found %A" t)

    member p.Base() : Expr =
        let ln = line ()
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
                | Code(src, l) ->
                    let sub = Parser(Array.ofList (Lexer.lex src l), fnames, nextId, imports)
                    sub.InterpExpr())
            |> EInterp
        | ID name when isFunc name && isSym ":" -> (advance (); ECall(name, p.ExprList()))
        | ID name when zeroArity.Contains name -> ECall(name, [])
        | ID name -> EVar name
        | SYM "(" ->
            let e = p.Expr()
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
        | SYM("%[" | "Q[" | "PQ[" as s) ->
            let items = if isSym "]" then [] else p.ExprList()
            expect "]"
            ECollLit((if s = "%[" then "set" else s.TrimEnd '['), items)
        | SYM "{" ->
            nextId.Value <- nextId.Value + 1
            let id = nextId.Value
            EBlock(id, p.Block "}")
        | t -> fail ln (sprintf "unexpected %A" t)

    member p.InterpExpr() =
        let e = p.Expr()
        while peek () = TERM do advance ()
        if peek () <> EOF then fail (line ()) "bad interpolation"
        e

/// Parses a file's tokens; `path` anchors relative imports.
let parse (tokens: Token list) (path: string) : Stmt list =
    let path = System.IO.Path.GetFullPath path
    let imports = { dir = System.IO.Path.GetDirectoryName path; seen = HashSet [ path ] }
    Parser(Array.ofList tokens, HashSet<string>(), ref 0, imports).Program()
