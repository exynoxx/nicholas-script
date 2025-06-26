module NS2.Ast

type AST =
    | Root of AST list
    | Int of int
    | String of string
    | Id of string
    | Binop of AST * string * AST    
    | Index of AST * AST    
    | Array of AST list
    | Call of string * AST list
    | Func of AST list
    | FuncCalled of AST list * AST list
    | NamedFunc of string * AST list
    | Map of AST * AST
    | Assign of AST * AST
    | Nop
    
    