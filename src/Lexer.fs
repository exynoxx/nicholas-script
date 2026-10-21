/// Stage 1: source -> tokens. All context-sensitive token decisions live here.
module Lexer

open System
open System.Text
open System.Collections.Generic
open Ast

let private symbols =
    [ ">>>="; ">>>"; "^^="; "**="; "++="; "<<="; ">>="; "^^"; "**"; "++"; "<<"; ">>"; "^="; "!="; "<="; ">="; ".."; "->"; "=>"; "+="; "-="; "*="; "/="; "%="; "&="; "|="; "??="; "??"
      "+"; "-"; "*"; "/"; "%"; "="; "<"; ">"; "&"; "|"; "^"; "?"; ":"; ","; "."; "("; ")"; "["; "]"; "{"; "}"; "#"; "!" ]

/// A block starting with one of these gets an implicit `_` on its left (operator section).
let private sectionOps = set [ "+"; "*"; "**"; "/"; "%"; "^^"; "++"; "^"; "<<"; ">>"; ">>>"; ">>>"; "="; "!="; "<"; ">"; "<="; ">="; "&"; "|" ]
let private openers = set [ "{"; "m{"; "f["; "/{"; "\\{"; "${" ]
let private foldOps = [ "+"; "*"; ">"; "<"; "&"; "|"; "^"; "-" ]

/// `startCol` is the column of src's first character (interpolations are lexed on their own).
let private opener (o: char) = match o with '?' -> "?[" | 'f' -> "[" | o -> string o

let lex (src: string) (startLine: int) (startCol: int) : Token list =
    let toks = ResizeArray<Token>()
    let stack = Stack<char * int * int>() // open bracket, its line and column
    let n = src.Length
    let mutable i = 0
    let mutable line = startLine
    let mutable lineStart = 1 - startCol // index of the current line's first character
    // where the token being lexed starts
    let mutable tokLine, tokCol = line, 0
    let mark () = (tokLine <- line; tokCol <- i - lineStart + 1)
    let mutable sectionPending = false
    let mutable stmtIndent = 0 // indentation of the line the current statement starts on
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
            | SYM s when sectionOps.Contains s -> toks.Add { tok = ID "_"; line = tokLine; col = tokCol }
            | _ -> ()
        toks.Add { tok = t; line = tokLine; col = tokCol }
    let sym s =
        add (SYM s)
        match s with
        | "(" -> stack.Push('(', tokLine, tokCol)
        | "f[" -> stack.Push('f', tokLine, tokCol) // a filter holds statements, like a block
        | "[" | "%[" | "Q[" | "PQ[" | "BT[" | "G[" | "DG[" -> stack.Push('[', tokLine, tokCol)
        | "{" | "m{" | "~{" | "/{" | "\\{" | "?{" | "${" -> stack.Push('{', tokLine, tokCol)
        | "?[" -> stack.Push('?', tokLine, tokCol)
        | ")" | "]" | "}" ->
            if stack.Count = 0 then failAt tokLine tokCol $"'{s}' has no matching opening bracket"
            let o, l, c = stack.Pop()
            let want = match o with '(' -> ")" | '[' | '?' | 'f' -> "]" | _ -> "}"
            if s <> want then failAt tokLine tokCol $"expected '{want}' to close the '{opener o}' at {l}:{c}, found '{s}'"
        | _ -> ()
        if openers.Contains s then sectionPending <- true
    let term () =
        match last () with
        | TERM -> ()
        | _ when toks.Count = 0 -> ()
        | _ -> add TERM
    let skipWs j =
        let mutable j = j
        while j < n && (src.[j] = ' ' || src.[j] = '\t') do j <- j + 1
        j
    /// `/op` is a fold, except `/-` followed by an operand (`10/-x`, `10/-#x`, `10/-.5`):
    /// `-` is the only fold operator that is also a prefix operator.
    let tryOpAfter () =
        foldOps |> List.tryFind (fun op ->
            if at 1 <> op.[0] then false
            elif op <> "-" then true
            else
                let k = skipWs (i + 2) - i
                let c = at k
                not (Char.IsLetterOrDigit c || "_$@\"(-".IndexOf c >= 0
                     || (c = '#' && " \t\r\n\000".IndexOf(at (k + 1)) < 0)
                     || (c = '.' && Char.IsAsciiDigit(at (k + 1))) || (c = '%' && at (k + 1) = '[')))
    /// From the '"' at j: the index of the closing '"' (escapes skipped), or n if there is none.
    let skipString j =
        let mutable j = j + 1
        while j < n && src.[j] <> '"' do j <- j + (if src.[j] = '\\' then 2 else 1)
        min j n
    /// At `?[`: is this `c ?[..] : d`, a ternary whose branch is an array literal? (else a `?[ ]` match filter)
    /// Not when the brackets can't hold an array literal (`?[>4]`, `?[1 => T; _]`), nor when an earlier `?` of
    /// this statement still waits for its `:` (`c ? xs?[1] : ys`: that `:` is the outer ternary's).
    let ternaryArray () =
        let mutable j, depth = i + 1, 0
        let mutable fin, arms = false, false
        while j < n && not fin do
            match src.[j] with
            | '"' -> j <- skipString j
            | '(' | '[' | '{' -> depth <- depth + 1
            | ')' | ']' | '}' -> (depth <- depth - 1; if depth = 0 then fin <- true)
            | ';' when depth = 1 -> arms <- true
            | '=' when depth = 1 && j + 1 < n && src.[j + 1] = '>' -> arms <- true
            | _ -> ()
            if not fin then j <- j + 1
        let f = skipWs (i + 2)
        let compares = "<>=".IndexOf(at (f - i)) >= 0 || (at (f - i) = '!' && at (f - i + 1) = '=')
        // `?`s at this bracket depth since the statement began that no later `:` answers (`println:` is not one)
        let mutable k, d, pending, colons = toks.Count - 1, 0, 0, 0
        while k >= 0 && toks.[k].tok <> TERM && d >= 0 do
            match toks.[k].tok with
            | SYM(")" | "]" | "}") -> d <- d + 1
            | SYM s when s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{" -> d <- d - 1
            | SYM "?" when d = 0 -> if colons > 0 then colons <- colons - 1 else pending <- pending + 1
            | SYM ":" when d = 0 -> colons <- colons + 1
            | _ -> ()
            k <- k - 1
        let k = skipWs (j + 1)
        k < n && src.[k] = ':' && not arms && not compares && pending <= 0
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
                    if i >= n then failAt tokLine tokCol "string interpolation '{' is never closed"
                    match src.[i] with
                    | '{' -> depth <- depth + 1
                    | '}' -> depth <- depth - 1
                    | '"' -> i <- skipString i
                    | _ -> ()
                    i <- i + 1
                parts.Add(Code(src.Substring(start, i - 1 - start), line, start - lineStart + 1))
            | c ->
                if c = '\n' then (line <- line + 1; lineStart <- i + 1)
                buf.Append c |> ignore
                i <- i + 1
        if i >= n then failAt tokLine tokCol "string is never closed (missing '\"')"
        i <- i + 1
        flush ()
        if parts.Count = 0 then parts.Add(Lit "")
        add (STR(List.ofSeq parts))
    let lexNumber () =
        let start = i
        if src.[i] = '-' then i <- i + 1
        if at 0 = '0' && (at 1 = 'x' || at 1 = 'b') then
            // 0xff / 0b101: 64-bit patterns, values >= 2^63 wrap to negative
            let radix = if at 1 = 'x' then 16 else 2
            let neg = src.[start] = '-'
            i <- i + 2
            let ds = i
            while Uri.IsHexDigit(at 0) && (radix = 16 || at 0 = '0' || at 0 = '1') do i <- i + 1
            if i = ds then failAt tokLine tokCol $"'0{src.[ds - 1]}' needs digits after it"
            let style = if radix = 16 then Globalization.NumberStyles.AllowHexSpecifier else Globalization.NumberStyles.AllowBinarySpecifier
            match UInt64.TryParse(src.AsSpan(ds, i - ds), style, null) with
            | true, v -> add (INT(if neg then -(int64 v) else int64 v))
            | _ -> failAt tokLine tokCol $"number '{src.Substring(start, i - start)}' doesn't fit in 64 bits"
        else
        while Char.IsAsciiDigit(at 0) do i <- i + 1
        let text = src.Substring(start, i - start)
        if at 0 = '.' && Char.IsAsciiDigit(at 1) then
            i <- i + 1
            while Char.IsAsciiDigit(at 0) do i <- i + 1
            let v = Double.Parse(src.Substring(start, i - start), Globalization.CultureInfo.InvariantCulture)
            if Double.IsInfinity v then failAt tokLine tokCol $"number '{src.Substring(start, i - start)}' is too large for a float"
            add (FLOAT v)
        else
            match Int64.TryParse text with
            | true, v -> add (INT v)
            | _ -> failAt tokLine tokCol $"number '{text}' is too large for an int (max {Int64.MaxValue})"
    while i < n do
        mark ()
        let c = src.[i]
        let digitNext k = Char.IsAsciiDigit(at k) || (at k = '.' && Char.IsAsciiDigit(at (k + 1)))
        if c = ' ' || c = '\t' || c = '\r' then i <- i + 1
        elif c = '\n' then
            line <- line + 1
            i <- i + 1
            lineStart <- i
            let inParens = stack.Count > 0 && (let o, _, _ = stack.Peek() in o <> '{' && o <> '?' && o <> 'f')
            let indent = skipWs i - i
            match last () with
            | SYM s when s.EndsWith "(" || s.EndsWith "[" || s.EndsWith "{" -> stmtIndent <- indent
            // a trailing operator continues the statement only onto a more indented line, so a
            // forgotten operand errors on its own line instead of swallowing the next statement;
            // a trailing `..` is an open-ended slice (`s@1..`), not an operator
            | SYM s when s <> ")" && s <> "]" && s <> "}" && (inParens || (indent > stmtIndent && s <> "..")) -> ()
            // so does a more indented line that starts with a binary operator or a map `{`
            | _ when indent > stmtIndent && endsExpr ()
                     && ("+-*/\\%=<>&|^?:{".IndexOf(at indent) >= 0 || (at indent = '!' && at (indent + 1) = '=')) -> ()
            | _ -> if not inParens then (term (); stmtIndent <- indent)
        elif c = '#' && (" \t\r\n\000".IndexOf(at 1) >= 0) then
            while i < n && src.[i] <> '\n' do i <- i + 1
        elif c = ';' then (term (); i <- i + 1)
        elif Char.IsAsciiDigit c || (c = '.' && Char.IsAsciiDigit(at 1) && not (endsExpr ())) then lexNumber ()
        elif c = '-' && digitNext 1 && not (endsExpr ()) then lexNumber ()
        elif c = '"' then lexString ()
        elif c = '$' && at 1 = '{' && endsExpr () then (sym "${"; i <- i + 2) // `x${key}` sorts by key
        elif c = '$' then (add (ID "$"); i <- i + 1)
        elif c = '_' then
            let start = i
            i <- i + 1
            while Char.IsAsciiDigit(at 0) do i <- i + 1
            add (ID(src.Substring(start, i - start)))
        elif Char.IsLetter c then
            let start = i
            while Char.IsLetterOrDigit(at 0) || at 0 = '_' do i <- i + 1
            let w = src.Substring(start, i - start)
            match w with
            | "T" -> add (BOOL true)
            | "F" -> add (BOOL false)
            | ("Q" | "PQ" | "BT" | "G" | "DG") when at 0 = '[' && not (endsExpr ()) -> (i <- i + 1; sym (w + "["))
            | _ -> add (ID w)
        elif c = '@' then
            if endsExpr () then
                if at 1 = '?' then (sym "@?"; i <- i + 2) else (sym "@"; i <- i + 1)
            else (add (ID "@"); i <- i + 1)
        elif (c = '/' || c = '\\') && endsExpr () && (let j = skipWs (i + 1) in j < n && src.[j] = '{') then
            // `x / {..}` is a fold too: dividing by a block can never type-check
            sym (string c + "{")
            i <- skipWs (i + 1) + 1
        elif (c = '/' || c = '\\') && endsExpr () && (tryOpAfter ()).IsSome then
            let op = (tryOpAfter ()).Value
            add (if c = '/' then FOLDOP op else SCANOP op)
            i <- i + 2
        elif c = '{' then (sym (if endsExpr () then "m{" else "{"); i <- i + 1)
        elif c = '~' && at 1 = '{' && endsExpr () then (sym "~{"; i <- i + 2)
        elif c = '?' && at 1 = '{' then (sym "?{"; i <- i + 2)
        elif c = '?' && at 1 = '[' && endsExpr () && not (ternaryArray ()) then (sym "?["; i <- i + 2)
        elif c = '[' then (sym (if endsExpr () then "f[" else "["); i <- i + 1)
        elif c = '%' && at 1 = '[' && not (endsExpr ()) then (sym "%["; i <- i + 2)
        else
            match symbols |> List.tryFind (fun s -> String.CompareOrdinal(src, i, s, 0, s.Length) = 0) with
            | Some s -> (sym s; i <- i + s.Length)
            | None -> failAt tokLine tokCol ($"unexpected character '{c}'")
    mark ()
    if stack.Count > 0 then
        let o, l, c = stack.Peek()
        failAt l c $"'{opener o}' is never closed"
    term ()
    add EOF
    List.ofSeq toks
