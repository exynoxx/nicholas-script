module NS2.Ast

type AST =
    | Int of int
    | Id of string
    | Binop of AST * string * AST    
    | Index of AST * AST    
    | Array of AST list
    | Func of AST list
    | Map of AST * AST
    