module NS2.Ast

open System.Collections.Generic
open NS2.Type

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
    | FuncCalled of AST list * AST
    | Func of string * AST
    | Map of AST * AST
    | Assign of AST * AST
    | Pipe of AST list
    | If of AST*AST*AST option
    | While of AST*AST
    
    | Typed of AST * Type
    | IfPhi of AST*AST*AST option*AST list
    | WhileExtra of AST*AST*Dictionary<string,Type>
    | WhilePhi of AST*AST*AST list
    | Phi of string * string * string
    | PhiSingle of string * string * string
    | Store of AST * AST
    | CreatePtr of AST * Type
    | Contract
    | Nop
    
