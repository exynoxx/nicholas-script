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

//TODO if ID func -> call node
let rec typecheck (tree:AST) =
    match tree with
    | Root body -> body |> List.map typecheck |> Root
    | Map (arr, func) ->
        if getType arr <> ArrayType then
            failwith "Mapping can only be done on arrays"
        if getType func <> FuncType then
            failwith "Can only map with a function"
        tree
    | Assign (Id id, body) ->
        let tbody = typecheck body
        match tbody with
        | Func tb -> NamedFunc (id, tb)
        | _ -> Assign (Id id, tbody)
        
    | x -> x