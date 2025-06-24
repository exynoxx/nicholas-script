module NS2.Ast

type AST =
    | Int of int
    | Str of string
    | Binop of AST * string * AST    
    | Array of AST list
    