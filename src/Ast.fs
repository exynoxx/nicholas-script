/// Tokens and the surface AST (stage 1-2 output).
module Ast

/// line, column (0 = unknown, -1 = end of the line), message
exception CompileError of int * int * string
let fail line msg = raise (CompileError(line, 0, msg))
let failAt line col msg = raise (CompileError(line, col, msg))

type Part =
    | Lit of string
    | Code of string * int * int // source, line, column

type Tok =
    | INT of int64
    | FLOAT of float
    | STR of Part list
    | BOOL of bool
    | ID of string    // names, plus "_", "_1", "$" and bare "@"
    | SYM of string   // operators and brackets; context forms: "f[" filter, "m{" map, "~{" while, "/{" "\{" fold/scan, "%[" "Q[" "PQ[" "BT[" "G[" "DG["
    | FOLDOP of string
    | SCANOP of string
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
    | EInterp of Expr list
    | EBool of bool
    | EVar of string
    | EArr of Expr list
    | ETuple of Expr list
    | EMapLit of (Expr * Expr) list
    | ECollLit of string * Expr list    // "set" | "Q" | "PQ" | "BT" | "G" | "DG"
    | ERange of Expr * Expr
    | EBin of string * Expr * Expr
    | EUn of string * Expr              // "-" | "!" | "#"
    | ETern of Expr * Expr * Expr
    | ECall of string * Expr list       // also x.f:a => ECall(f, [x; a])
    | EIndex of Expr * Expr
    | EHas of Expr * Expr
    | EIter of IterKind * Expr * Stmt list
    | EIterOp of IterKind * Expr * string // fold or scan with an operator
    | EBlock of int * string list list * Stmt list // id, named params (`f a (b, c) = …`; a group unpacks), body
    | EGroup of Stmt list               // (s1; s2; value)
    | EMatch of string * Expr * Expr    // "map" `x ?{}` | "filter" `x ?[]` | "whole" (array-literal pattern): subject, arms (`_` = subject/element)

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
    | FOLDOP s -> $"FOLDOP {s}"
    | SCANOP s -> $"SCANOP {s}"
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
    | FOLDOP s -> $"'/{s}'"
    | SCANOP s -> $"'\\{s}'"
    | TERM -> "end of line"
    | EOF -> "end of file"

let rec showExpr (e: Expr) : string =
    let list xs = xs |> List.map showExpr |> String.concat "; "
    match e with
    | EInt v -> $"EInt {v}"
    | EFloat v -> $"EFloat {v}"
    | EStr s -> "EStr " + quote s
    | EInterp xs -> $"EInterp [{list xs}]"
    | EBool b -> $"EBool {b}"
    | EVar n -> $"EVar {n}"
    | EArr xs -> $"EArr [{list xs}]"
    | ETuple xs -> $"ETuple [{list xs}]"
    | EMapLit ps -> "EMapLit [" + (ps |> List.map (fun (k, v) -> showExpr k + " -> " + showExpr v) |> String.concat "; ") + "]"
    | ECollLit(k, xs) -> $"ECollLit {k} [{list xs}]"
    | ERange(a, b) -> $"ERange({showExpr a}, {showExpr b})"
    | EBin(op, a, b) -> $"EBin({op}, {showExpr a}, {showExpr b})"
    | EUn(op, x) -> $"EUn({op}, {showExpr x})"
    | ETern(c, a, b) -> $"ETern({showExpr c}, {showExpr a}, {showExpr b})"
    | ECall(f, xs) -> $"ECall({f}, [{list xs}])"
    | EIndex(x, i) -> $"EIndex({showExpr x}, {showExpr i})"
    | EHas(x, k) -> $"EHas({showExpr x}, {showExpr k})"
    | EIter(k, src, body) -> $"EIter({showKind k}, {showExpr src}, {showStmts body})"
    | EIterOp(k, src, op) -> $"EIterOp({showKind k}, {showExpr src}, {op})"
    | EBlock(id, ps, body) -> $"EBlock({id}, {ps}, {showStmts body})"
    | EGroup body -> $"EGroup {showStmts body}"
    | EMatch(kind, x, arms) -> $"EMatch({kind}, {showExpr x}, {showExpr arms})"

and showStmt (s: Stmt) : string =
    let list xs = xs |> List.map showExpr |> String.concat ", "
    match s with
    | SAssign(lvs, es) -> $"SAssign([{list lvs}], [{list es}])"
    | SCompound(op, lv, e) -> $"SCompound({op}, {showExpr lv}, {showExpr e})"
    | SWhile(c, body) -> $"SWhile({showExpr c}, {showStmts body})"
    | SExpr e -> $"SExpr {showExpr e}"
    | SLine n -> $"SLine {n}"

and showStmts (ss: Stmt list) = "[" + (ss |> List.filter (function SLine _ -> false | _ -> true) |> List.map showStmt |> String.concat "; ") + "]"
