module NS2.Ast

type AST =
    | Root of AST list
    | Block of AST list
    | Int of int
    | String of string
    | Bool of bool
    | Id of string
    | Binop of AST * string * AST    
    | Unop of string * AST    
    | Index of AST * AST    
    | Array of AST list
    | Call of string * AST list
    | Func of AST
    | FuncCalled of AST list * AST
    | NamedFunc of string * AST
    | Map of AST * AST
    | Assign of AST * AST
    | Pipe of AST list
    | If of AST*AST*AST option
    | While of AST*AST
    | Nop
    
    