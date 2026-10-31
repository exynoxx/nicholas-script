/// Tokens and the surface AST (stage 1-2 output).
module Ast

/// line, column (0 = unknown, -1 = end of the line), message
exception CompileError of int * int * string
let fail line msg = raise (CompileError(line, 0, msg))
let failAt line col msg = raise (CompileError(line, col, msg))

/// Source (line, column) of AST and core nodes, by reference: the parser marks nodes, desugar copies the marks
/// to the core nodes it builds, and the typer reports its errors there.
let private positions = System.Runtime.CompilerServices.ConditionalWeakTable<obj, int * int>()
let setPos (x: obj) (p: int * int) = positions.AddOrUpdate(x, p)
let posOf (x: obj) = match positions.TryGetValue x with | true, p -> Some p | _ -> None

/// Lines of the n-th imported file are offset by n * fileLines, so an error's line also names its file.
/// Per thread: the test runner compiles files in parallel and reports each error on the thread that compiled it.
let fileLines = 1_000_000
let imported = new System.Threading.ThreadLocal<ResizeArray<string>>(fun () -> ResizeArray())
/// The imported file (as a path from the working directory) and its own line, for an offset line.
let source line = if line >= fileLines then Some(imported.Value.[line / fileLines - 1], line % fileLines) else None
let showLine line = match source line with Some(path, l) -> $"{path}:{l}" | None -> string line

/// " (did you mean 'x'?)" for the closest candidate within edit distance 1 (2 for names of 8+ letters, none under 3), else "";
/// swapping two adjacent letters counts as one edit.
let didYouMean (name: string) (candidates: seq<string>) =
    let dist (a: string) (b: string) =
        let d = Array2D.init (a.Length + 1) (b.Length + 1) (fun i j -> if i = 0 then j elif j = 0 then i else 0)
        for i in 1 .. a.Length do
            for j in 1 .. b.Length do
                d.[i, j] <- min (min (d.[i - 1, j] + 1) (d.[i, j - 1] + 1)) (d.[i - 1, j - 1] + (if a.[i - 1] = b.[j - 1] then 0 else 1))
                if i > 1 && j > 1 && a.[i - 1] = b.[j - 2] && a.[i - 2] = b.[j - 1] then d.[i, j] <- min d.[i, j] (d.[i - 2, j - 2] + 1)
        d.[a.Length, b.Length]
    let limit = if name.Length >= 8 then 2 elif name.Length >= 3 then 1 else 0
    candidates
    |> Seq.filter (fun c -> c <> name && not (c.Contains '%') && not (c.StartsWith "_"))
    |> Seq.map (fun c -> dist name c, c)
    |> Seq.filter (fun (d, _) -> d <= limit)
    |> Seq.sortBy (fun (d, c) -> d, c.[0] <> name.[0])
    |> Seq.tryHead
    |> Option.map (fun (_, c) -> $" (did you mean '{c}'?)")
    |> Option.defaultValue ""

/// Prefixes on ECall names the user wrote, so the typer can let a variable in scope shadow the function:
/// `'f` is `f:args` (a non-function variable `f` makes it an error), `?f` a bare name (`x{f}`, `read`: the variable wins),
/// `!f` the negated function `!f` whose one argument is the block `{!f:_}` (a variable `f` makes it a plain `!f`).
let callMark, nameMark, negMark = "'", "?", "!"

type Part =
    | Lit of string
    | Code of string * int * int // source, line, column

type Tok =
    | INT of int64
    | FLOAT of float
    | STR of Part list
    | BOOL of bool
    | ID of string    // names, plus "_", "_1", "$" and bare "@"
    | SYM of string   // operators and brackets; context forms: "f[" filter, "m{" map, "~{" while, plus the features' own
    | TERM
    | EOF

type Token = { tok: Tok; line: int; col: int }

type IterKind =
    | IMap
    | IFilter
    | IFold
    | IScan
    | IEach // a map whose result is discarded

let showKind k = match k with IMap -> "map" | IFilter -> "filter" | IFold -> "fold" | IScan -> "scan" | IEach -> "each"

type Expr =
    | EInt of int64
    | EFloat of float
    | EStr of string
    | EBool of bool
    | EVar of string
    | EArr of Expr list
    | ETuple of Expr list
    | EMapLit of (Expr * Expr) list
    | ERange of Expr * Expr
    | EBin of string * Expr * Expr
    | EUn of string * Expr              // "-" | "!" | "#"
    | ETern of Expr * Expr * Expr
    | ECall of string * Expr list       // also x.f:a => ECall(f, [x; a])
    | EIndex of Expr * Expr
    | EHas of Expr * Expr
    | EIter of IterKind * Expr * Stmt list
    | EBlock of int * string list list * Stmt list // id, named params (`f a (b, c) = …`; a group unpacks), body
    | EGroup of Stmt list               // (s1; s2; value)
    | EExt of string * Expr list * Stmt list list // a feature's construct: name (its Desugar.ext key), operands, blocks

and Stmt =
    | SAssign of Expr list * Expr list
    | SCompound of string * Expr * Expr
    | SWhile of Expr * Stmt list
    | SExpr of Expr
    | SLine of int

// ---------- printers (explicit, so the compiler needs no reflection: native AOT) ----------

let quote (s: string) = "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""

let showTok (t: Tok) =
    match t with
    | INT v -> $"INT {v}"
    | FLOAT v -> $"FLOAT {v}"
    | STR parts -> "STR " + (parts |> List.map (function Lit s -> quote s | Code(c, _, _) -> "{" + c + "}") |> String.concat "")
    | BOOL b -> if b then "BOOL T" else "BOOL F"
    | ID s -> $"ID {s}"
    | SYM s -> $"SYM {s}"
    | TERM -> "TERM"
    | EOF -> "EOF"

/// A token as an error message shows it: its source text, or a description.
let describeTok (t: Tok) =
    match t with
    | INT v -> $"'{v}'"
    | FLOAT v -> $"'{v}'"
    | STR _ -> "a string"
    | BOOL b -> if b then "'T'" else "'F'"
    | ID s -> $"'{s}'"
    | SYM("f[" | "m{" as s) -> $"'{s.Substring 1}'"
    | SYM s -> $"'{s}'"
    | TERM -> "end of line"
    | EOF -> "end of file"

let rec showExpr (e: Expr) : string =
    let list xs = xs |> List.map showExpr |> String.concat "; "
    match e with
    | EInt v -> $"EInt {v}"
    | EFloat v -> $"EFloat {v}"
    | EStr s -> "EStr " + quote s
    | EBool b -> $"EBool {b}"
    | EVar n -> $"EVar {n}"
    | EArr xs -> $"EArr [{list xs}]"
    | ETuple xs -> $"ETuple [{list xs}]"
    | EMapLit ps -> "EMapLit [" + (ps |> List.map (fun (k, v) -> showExpr k + " -> " + showExpr v) |> String.concat "; ") + "]"
    | ERange(a, b) -> $"ERange({showExpr a}, {showExpr b})"
    | EBin(op, a, b) -> $"EBin({op}, {showExpr a}, {showExpr b})"
    | EUn(op, x) -> $"EUn({op}, {showExpr x})"
    | ETern(c, a, b) -> $"ETern({showExpr c}, {showExpr a}, {showExpr b})"
    | ECall(f, xs) -> $"ECall({f}, [{list xs}])"
    | EIndex(x, i) -> $"EIndex({showExpr x}, {showExpr i})"
    | EHas(x, k) -> $"EHas({showExpr x}, {showExpr k})"
    | EIter(k, src, body) -> $"EIter({showKind k}, {showExpr src}, {showStmts body})"
    | EBlock(id, ps, body) -> $"EBlock({id}, {ps}, {showStmts body})"
    | EGroup body -> $"EGroup {showStmts body}"
    | EExt(name, xs, blocks) -> $"EExt({name}, [{list xs}]" + (blocks |> List.map (fun b -> ", " + showStmts b) |> String.concat "") + ")"

and showStmt (s: Stmt) : string =
    let list xs = xs |> List.map showExpr |> String.concat ", "
    match s with
    | SAssign(lvs, es) -> $"SAssign([{list lvs}], [{list es}])"
    | SCompound(op, lv, e) -> $"SCompound({op}, {showExpr lv}, {showExpr e})"
    | SWhile(c, body) -> $"SWhile({showExpr c}, {showStmts body})"
    | SExpr e -> $"SExpr {showExpr e}"
    | SLine n -> $"SLine {n}"

and showStmts (ss: Stmt list) = "[" + (ss |> List.filter (function SLine _ -> false | _ -> true) |> List.map showStmt |> String.concat "; ") + "]"
