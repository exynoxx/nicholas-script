module NS2.Interpreter

open System
open System.Collections.Generic
open NS2.Ast
open NS2.StdLib


type Scope (parent: Scope option) =
    
    let vars = Dictionary<string, AST>()
    let funcs = Dictionary<string, AST list>()

    member this.GetVariable(name: string) : AST option =
        match vars.ContainsKey name with
        | true -> Some vars[name]
        | _ ->
            match parent with
            | Some p -> p.GetVariable(name)
            | None -> None
            
    member this.GetFunction(name: string) : AST list option =
        match funcs.ContainsKey name with
        | true -> Some funcs[name]
        | _ ->
            match parent with
            | Some p -> p.GetFunction(name)
            | None -> None

    member this.SetVar(name: string, value: AST)= vars[name] <- value
    member this.SetFunc(name: string, value: AST list)= funcs[name] <- value
    member this.Push() : Scope = Scope(Some this)
    static member Empty = Scope(None)

let rec eval_internal (scope: Scope) (ast: AST) =
    match ast with
    | Int n -> Int n
    | String x -> String x
    | Id name ->
        match scope.GetVariable name with
        | Some v -> v
        | None ->
            //TODO replace id with call node in typechecker
            match scope.GetFunction name with
            | Some _ -> eval_internal scope (Call (name, []))
            | None -> failwith $"Unbound identifier: {name}"
        
    | Assign (Id id, bodyExpr) ->
        let body = eval_internal scope bodyExpr
        scope.SetVar(id, body)
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
        scope.SetFunc(id,body)
        ast
    | FuncCalled (args, fbody) ->
        let bodyScope = scope.Push()
        args |> List.mapi (fun i x -> bodyScope.SetVar($"${i+1}", x)) |> ignore
        let results = fbody |> List.map (eval_internal bodyScope)
        results |> List.last
            
    | Call (id, args) ->
        let callArgs = args |> List.map (eval_internal scope)
        match eval_std_function (id, callArgs) with
        | Some result -> result
        | None -> 
            match scope.GetFunction id with
            | Some fbody ->
                let bodyScope = scope.Push()
                args |> List.mapi (fun i x -> bodyScope.SetVar($"${i+1}", x)) |> ignore
                let results = fbody |> List.map (eval_internal bodyScope)
                results |> List.last
            | None -> failwith $"Function {id} not found"

    | Func _ -> failwith "Func should not exist in this stage"
    | Map _ -> failwith "Map should not exist in this stage"
    | _ -> failwith $"Unsupported %A{ast}"

let rec toString (scope: Scope) (ast:AST) : string =
    match ast with
    | Int n -> n.ToString()
    | String x -> x
    | Array elements ->
        let content = elements |> List.map (toString scope)
        "[" + String.Join (", ", content) + "]"
        
    | Id name ->
        match scope.GetVariable name with
        | Some v -> toString scope v
        | None -> failwith "%A not defined" name
     
    | Binop (left, op, right) -> failwith "Unsupported"
    | Index (arrExpr, indexExpr) -> failwith "Unsupported"
    | _ -> failwith $"toString Unsupported %A{ast}"
    
let rec eval =
    function
    | Root expressions -> 
        let scope = Scope.Empty
        let mutable result = null
        
        for e in expressions do
            result <- eval_internal scope e
        
        printfn "%s" (toString scope result)
    | _ -> failwith "Eval of non root node"
