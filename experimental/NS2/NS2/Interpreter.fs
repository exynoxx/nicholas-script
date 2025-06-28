module NS2.Interpreter

open System
open NS2.Ast
open NS2.Scope
open NS2.StdLib



let rec eval_internal (scope: Scope) (ast: AST) =
    match ast with
    | Int n -> Int n
    | String x -> String x
    | Id name ->
        
        let real_name =
            match scope.GetAlias name with
            | Some alias -> alias
            | None -> name
        
        match scope.GetVariable real_name with
        | Some v -> v
        | None ->
            //TODO replace id with call node in typechecker
            match scope.GetFunction real_name with
            | Some _ -> eval_internal scope (Call (real_name, []))
            | None -> failwith $"Unbound identifier: {real_name}({name})"
        
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
                args |> List.map (eval_internal bodyScope) |> List.mapi (fun i x -> bodyScope.SetVar($"${i+1}", x)) |> ignore
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
     
    | Nop -> ""
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
