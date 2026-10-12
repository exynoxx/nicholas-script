/// Stage 1: source -> tokens. All context-sensitive token decisions live here.
module Lexer

open System
open System.Text
open System.Collections.Generic
open Ast

let private symbols =
    [ "^^="; "++="; "<<="; ">>="; "^^"; "++"; "<<"; ">>"; "^="; "~="; "<="; ">="; ".."; "->"; "+="; "-="; "*="; "/="; "%="; "&="; "|="
      "+"; "-"; "*"; "/"; "%"; "="; "<"; ">"; "&"; "|"; "^"; "?"; ":"; ","; "."; "("; ")"; "["; "]"; "{"; "}"; "#"; "~" ]

/// A block starting with one of these gets an implicit `_` on its left (operator section).
let private sectionOps = set [ "+"; "*"; "/"; "%"; "^^"; "++"; "^"; "<<"; ">>"; "="; "~="; "<"; ">"; "<="; ">="; "&"; "|" ]
let private openers = set [ "{"; "m{"; "f["; "/{"; "\\{" ]
let private foldOps = [ "+"; "*"; ">"; "<"; "&"; "|"; "^"; "-" ]

let lex (src: string) (startLine: int) : Token list =
    let toks = ResizeArray<Token>()
    let stack = Stack<char>()
    let n = src.Length
    let mutable i = 0
    let mutable line = startLine
    let mutable sectionPending = false
    let at k = if i + k < n then src.[i + k] else '\000'
    let last () = if toks.Count = 0 then TERM else toks.[toks.Count - 1].tok
    let endsExpr () =
        match last () with
        | INT _ | FLOAT _ | STR _ | BOOL _ | ID _ | FOLDOP _ | SCANOP _ -> true
        | SYM (")" | "]" | "}") -> true
        | _ -> false
    let add t =
        if sectionPending then
            sectionPending <- false
            match t with
            | SYM s when sectionOps.Contains s -> toks.Add { tok = ID "_"; line = line }
            | _ -> ()
        toks.Add { tok = t; line = line }
    let sym s =
        add (SYM s)
        match s with
        | "(" -> stack.Push '('
        | "[" | "f[" | "%[" | "Q[" | "PQ[" -> stack.Push '['
        | "{" | "m{" | "~{" | "/{" | "\\{" -> stack.Push '{'
        | ")" | "]" | "}" -> if stack.Count > 0 then stack.Pop() |> ignore
        | _ -> ()
        if openers.Contains s then sectionPending <- true
    let term () =
        match last () with
        | TERM -> ()
        | _ when toks.Count = 0 -> ()
        | _ -> add TERM
    let isTerminator c = c = '\000' || "\n\r;)]},.{[/\\#".IndexOf(c: char) >= 0
    /// `/op` is a fold only when no operand follows the operator.
    let tryOpAfter () =
        foldOps |> List.tryFind (fun op ->
            if at 1 <> op.[0] then false
            else
                let mutable j = i + 2
                while j < n && (src.[j] = ' ' || src.[j] = '\t') do j <- j + 1
                isTerminator (if j < n then src.[j] else '\000'))
    let lexString () =
        i <- i + 1
        let parts = ResizeArray<Part>()
        let buf = StringBuilder()
        let flush () = if buf.Length > 0 then (parts.Add(Lit(buf.ToString())); buf.Clear() |> ignore)
        while i < n && src.[i] <> '"' do
            match src.[i] with
            | '\\' ->
                let c = at 1
                buf.Append(match c with 'n' -> '\n' | 't' -> '\t' | c -> c) |> ignore
                i <- i + 2
            | '{' ->
                flush ()
                let start = i + 1
                let mutable depth = 1
                i <- i + 1
                while depth > 0 do
                    if i >= n then fail line "unterminated interpolation"
                    match src.[i] with
                    | '{' -> depth <- depth + 1
                    | '}' -> depth <- depth - 1
                    | '"' -> (i <- i + 1; while i < n && src.[i] <> '"' do i <- i + 1)
                    | _ -> ()
                    i <- i + 1
                parts.Add(Code(src.Substring(start, i - 1 - start), line))
            | c ->
                if c = '\n' then line <- line + 1
                buf.Append c |> ignore
                i <- i + 1
        if i >= n then fail line "unterminated string"
        i <- i + 1
        flush ()
        if parts.Count = 0 then parts.Add(Lit "")
        add (STR(List.ofSeq parts))
    let lexNumber () =
        let start = i
        if src.[i] = '-' then i <- i + 1
        while Char.IsDigit(at 0) do i <- i + 1
        if at 0 = '.' && Char.IsDigit(at 1) then
            i <- i + 1
            while Char.IsDigit(at 0) do i <- i + 1
            add (FLOAT(Double.Parse(src.Substring(start, i - start), Globalization.CultureInfo.InvariantCulture)))
        else add (INT(Int64.Parse(src.Substring(start, i - start))))
    while i < n do
        let c = src.[i]
        let digitNext k = Char.IsDigit(at k) || (at k = '.' && Char.IsDigit(at (k + 1)))
        if c = ' ' || c = '\t' || c = '\r' then i <- i + 1
        elif c = '\n' then
            line <- line + 1
            i <- i + 1
            let inParens = stack.Count > 0 && stack.Peek() <> '{'
            let continues = match last () with SYM s -> s <> ")" && s <> "]" && s <> "}" | _ -> false
            if not inParens && not continues then term ()
        elif c = '#' && (" \t\r\n\000".IndexOf(at 1) >= 0) then
            while i < n && src.[i] <> '\n' do i <- i + 1
        elif c = ';' then (term (); i <- i + 1)
        elif Char.IsDigit c || (c = '.' && Char.IsDigit(at 1) && not (endsExpr ())) then lexNumber ()
        elif c = '-' && digitNext 1 && not (endsExpr ()) then lexNumber ()
        elif c = '"' then lexString ()
        elif c = '$' then (add (ID "$"); i <- i + 1)
        elif c = '_' then
            let start = i
            i <- i + 1
            while Char.IsDigit(at 0) do i <- i + 1
            add (ID(src.Substring(start, i - start)))
        elif Char.IsLetter c then
            let start = i
            while Char.IsLetterOrDigit(at 0) do i <- i + 1
            let w = src.Substring(start, i - start)
            match w with
            | "T" -> add (BOOL true)
            | "F" -> add (BOOL false)
            | ("Q" | "PQ") when at 0 = '[' && not (endsExpr ()) -> (i <- i + 1; sym (w + "["))
            | _ -> add (ID w)
        elif c = '@' then
            if endsExpr () then
                if at 1 = '?' then (sym "@?"; i <- i + 2) else (sym "@"; i <- i + 1)
            else (add (ID "@"); i <- i + 1)
        elif (c = '/' || c = '\\') && endsExpr () && at 1 = '{' then (sym (string c + "{"); i <- i + 2)
        elif (c = '/' || c = '\\') && endsExpr () && (tryOpAfter ()).IsSome then
            let op = (tryOpAfter ()).Value
            add (if c = '/' then FOLDOP op else SCANOP op)
            i <- i + 2
        elif c = '{' then (sym (if endsExpr () then "m{" else "{"); i <- i + 1)
        elif c = '~' && at 1 = '{' && endsExpr () then (sym "~{"; i <- i + 2)
        elif c = '[' then
            let adjacent = i > 0 && not (Char.IsWhiteSpace src.[i - 1])
            sym (if endsExpr () && adjacent then "f[" else "[")
            i <- i + 1
        elif c = '%' && at 1 = '[' && not (endsExpr ()) then (sym "%["; i <- i + 2)
        else
            match symbols |> List.tryFind (fun s -> String.CompareOrdinal(src, i, s, 0, s.Length) = 0) with
            | Some s -> (sym s; i <- i + s.Length)
            | None -> fail line (sprintf "unexpected character '%c'" c)
    term ()
    add EOF
    List.ofSeq toks
