module NS2.TypeChecker

open NS2.Ast

type typ =
    | Unknown
    | IntType
    | StringType
    | ArrayType
    | FuncType

let getType =
    function
    | Int _ -> IntType
    | Id _ -> Unknown
    | Array _ -> ArrayType
    | x -> failwith "Not recognized %A" x

let typecheck (tree:AST) =
    match tree with
    | Map (arr, func) ->
        if getType arr <> ArrayType then
            failwith "Mapping can only be done on arrays"
        if getType func <> FuncType then
            failwith "Can only map with a function"
        tree
    | x -> x