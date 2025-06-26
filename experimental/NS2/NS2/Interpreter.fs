module NS2.Interpreter

open System
open System.Collections.Generic
open System.Linq.Expressions
open NS2.Ast

type Scope (parent: Scope option) =
    
    let vars = Dictionary<string, AST>()
    let funcs = Dictionary<string, AST list>()
    
    member _.Parent = parent
    member _.Vars = vars
    member _.Func s = vars

    member this.Get(name: string) : AST option =
        match vars.ContainsKey name with
        | true -> Some vars[name]
        | _ ->
            match parent with
            | Some p -> p.Get(name)
            | None -> None

    member this.Set(name: string, value: AST)= vars[name] <- value
    member this.Push() : Scope = Scope(Some this)
    static member Empty = Scope(None)

let rec eval_internal (scope: Scope) (ast: AST) =
    match ast with
    | Int n -> Int n
    | Id name ->
        match scope.Get name with
        | Some v -> v
        | None -> failwith "Unbound identifier: %s" name
        
    | Assign (Id id, bodyExpr) ->
        let body = eval_internal scope bodyExpr
        scope.Set(id, body)
        body
        
    | Binop (left, op, right) ->
        let l = eval_internal scope left
        let r = eval_internal scope right
        match (l, r) with
        | Int x, Int y ->
            let result = 
                match op with
                | "+" -> x + y
                | "-" -> x - y
                | "*" -> x * y
                | "/" -> x / y
                | "**" -> (int)(Math.Pow(x,y))
                | _ -> failwith "Binop op not supported %s" op
            Int result
        | _ -> failwith "Binop types other than int not supported for now"

    | Array elements -> elements |> List.map (eval_internal scope) |> Array

    | Index (arrExpr, indexExpr) ->
        let arrVal = eval_internal scope arrExpr
        let idxVal = eval_internal scope indexExpr
        match arrVal, idxVal with
        | Array arr, Int idx when idx >= 0 && idx < List.length arr -> arr[idx]
        | Array arr, Int idx -> failwith "Array index outside range"
        | _ -> failwith "Invalid array indexing types"

    | NamedFunc (id,body) ->
        //elements |> List.map (eval_internal scope)
        ast
    
    | Func paramsAndBody ->
        //elements |> List.map (eval_internal scope)
        ast

    | Map (arr, f) -> ast
        (*let earr = eval_internal scope arr
        let ef = eval_internal scope f
        match (earr, ef) with
        | (Array a, Func f) ->
            ()
        | _ -> failwith "Cannot map non array"
        
        
        let collection = eval env collectionExpr
        match collection with
        | ArrayVal items ->
            match lambdaExpr with
            | Func [Id param; body] ->
                items
                |> List.map (fun v ->
                    let newEnv = scope.Add(param, v)
                    eval newEnv body)
                |> ArrayVal
            | _ -> failwith "Invalid lambda for map"
        | _ -> failwith "Map expects array as first argument"
        *)
        
    | _ -> failwith "Unsupported %A" ast

let rec toString (scope: Scope) (ast:AST) : string =
    match ast with
    | Int n -> n.ToString()
    | Array elements ->
        let content = elements |> List.map (toString scope)
        "[" + String.Join (", ", content) + "]"
        
    | Id name ->
        match scope.Get name with
        | Some v -> toString scope v
        | None -> failwith "%A not defined" name
     
    | Binop (left, op, right) -> failwith "Unsupported"
    | Index (arrExpr, indexExpr) -> failwith "Unsupported"
    | _ -> failwith "toString Unsupported %A " ast
    
let rec eval =
    function
    | Root expressions -> 
        let scope = Scope.Empty
        let mutable result = null
        
        for e in expressions do
            result <- eval_internal scope e
        
        printfn "%s" (toString scope result)
    | _ -> failwith "Eval of non root node"
