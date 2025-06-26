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

let rec typecheck (tree:AST) =
    match tree with
    | Root body -> body |> List.map typecheck |> Root
    
    //TODO pullout anon func to random var, make arr of f calls, eliminate namedCall
    | Map (arr, func) ->
        match (arr, func) with
        | Array a , Func f ->
            let elements = a |> List.map (fun x -> FuncCalled ([x], f))
            Array elements
        | _ -> failwith "Can only map an array with a function"
        
    | Assign (Id id, body) ->
        let tbody = typecheck body
        match tbody with
        | Func tb -> NamedFunc (id, tb)
        | _ -> Assign (Id id, tbody)
        
    | x -> x