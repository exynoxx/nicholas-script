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
