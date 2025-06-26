module NS2.Interpreter

open System
open System.Collections.Generic
open NS2.Ast

type Scope (parent: Scope option) =
    
    let vars = Dictionary<string, AST>()
    let funcs = Dictionary<string, AST list>()
    
    member _.Parent = parent
    member _.Vars = vars
    member _.Funcs = vars

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
        | None -> failwithf "Unbound identifier: %s" name
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

let rec toString (ast:AST) : string =
    match ast with
    | Int n -> n.ToString()
    | Array elements ->
        let content = elements |> List.map toString
        "[" + String.Join (", ", content) + "]"
        
    | Id name -> failwith "Unsupported"
    | Binop (left, op, right) -> failwith "Unsupported"
    | Index (arrExpr, indexExpr) -> failwith "Unsupported"
    | _ -> failwith "%A Unsupported" ast
    
let rec eval (ast:AST) =
    let scope = Scope.Empty
    let evaluated = eval_internal scope ast
    
    printfn "%s" (toString evaluated)
