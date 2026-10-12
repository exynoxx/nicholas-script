/// Tokens and the surface AST (stage 1-2 output).
module Ast

exception CompileError of int * string
let fail line msg = raise (CompileError(line, msg))

type Part =
    | Lit of string
    | Code of string * int // source, line

type Tok =
    | INT of int64
    | FLOAT of float
    | STR of Part list
    | BOOL of bool
    | ID of string    // names, plus "_", "_1", "$" and bare "@"
    | SYM of string   // operators and brackets; context forms: "f[" filter, "m{" map, "~{" while, "/{" "\{" fold/scan, "%[" "Q[" "PQ["
    | FOLDOP of string
    | SCANOP of string
    | TERM
    | EOF

type Token = { tok: Tok; line: int }

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
    | ECollLit of string * Expr list    // "set" | "Q" | "PQ"
    | ERange of Expr * Expr
    | EBin of string * Expr * Expr
    | EUn of string * Expr              // "-" | "~" | "#"
    | ETern of Expr * Expr * Expr
    | ECall of string * Expr list       // also x.f:a => ECall(f, [x; a])
    | EIndex of Expr * Expr
    | EHas of Expr * Expr
    | EIter of string * Expr * Stmt list // "map" | "filter" | "fold" | "scan"
    | EIterOp of string * Expr * string  // "fold" | "scan", operator
    | EBlock of int * Stmt list

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
    | STR parts -> "STR " + (parts |> List.map (function Lit s -> quote s | Code(c, _) -> "{" + c + "}") |> String.concat "")
    | BOOL b -> if b then "BOOL T" else "BOOL F"
    | ID s -> $"ID {s}"
    | SYM s -> $"SYM {s}"
    | FOLDOP s -> $"FOLDOP {s}"
    | SCANOP s -> $"SCANOP {s}"
    | TERM -> "TERM"
    | EOF -> "EOF"

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
    | EIter(k, src, body) -> $"EIter({k}, {showExpr src}, {showStmts body})"
    | EIterOp(k, src, op) -> $"EIterOp({k}, {showExpr src}, {op})"
    | EBlock(id, body) -> $"EBlock({id}, {showStmts body})"

and showStmt (s: Stmt) : string =
    let list xs = xs |> List.map showExpr |> String.concat ", "
    match s with
    | SAssign(lvs, es) -> $"SAssign([{list lvs}], [{list es}])"
    | SCompound(op, lv, e) -> $"SCompound({op}, {showExpr lv}, {showExpr e})"
    | SWhile(c, body) -> $"SWhile({showExpr c}, {showStmts body})"
    | SExpr e -> $"SExpr {showExpr e}"
    | SLine n -> $"SLine {n}"

and showStmts (ss: Stmt list) = "[" + (ss |> List.filter (function SLine _ -> false | _ -> true) |> List.map showStmt |> String.concat "; ") + "]"
