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
        
        match (scope, real_name) with
        | Variable value -> value
        | Function _ -> eval_internal scope (Call (real_name, []))
        | IsStdFunction _ -> eval_internal scope (Call (real_name, []))
        | _ -> failwith $"eval_internal Unbound identifier: {real_name}({name})"

    | Assign (Id id, Id other) ->
        match (scope, other) with
        | IsStdFunction -> ()
        | Function _ -> scope.SetAlias(id,other)
        | Variable value -> scope.SetVar(id, value)
        | _ -> failwith $"Variable {other} does not exist"
        Assign (Id id, Id other)
        
    | Assign (Id id, bodyExpr) ->
        let body = eval_internal scope bodyExpr
        scope.SetVar(id, body)
        Nop

    | Unaryop (op, right) ->
        failwith "Unary not implemented"
        
    | Binop (left, op, right) ->
        let l = eval_internal scope left
        let r = eval_internal scope right
        match (l, r) with
        | Int x, Int y ->
             match op with
                | "+" -> Int (x + y)
                | "-" -> Int (x - y)
                | "*" -> Int (x * y)
                | "/" -> Int (x / y)
                | ">" -> Bool (x > y)
                | "<" -> Bool (x < y)
                | "<=" -> Bool (x <= y)
                | ">=" -> Bool (x >= y)
                | "==" -> Bool (x = y)
                | "!=" -> Bool (x <> y)
                | "**" -> Int (int(Math.Pow(x,y))) //TODO move to std lib
                | _ -> failwith $"Binop op not supported %A{op}" 
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
         
    | Pipe (x::xs) ->
        
        let rec folder acc =
            function
            | Id f ->
                
                let real_name =
                    match scope.GetAlias f with
                    | Some alias -> alias
                    | None -> f
                
                match scope.GetFunction real_name with
                | Some _ -> eval_internal scope (Call (real_name, [acc]))
                | None -> failwith $"Pipeting to variable does not makesense"
                
            | Func f -> eval_internal scope (FuncCalled ([acc], Func f))
            | Call (id,args) -> eval_internal scope (Call (id, args@[acc]))
            | x -> failwith $"eval pipe stage not supported %A{x}"
        
        let first =  eval_internal scope x
        xs |> List.fold folder first
        
    | FuncCalled (args, Func (Block body)) ->
        let bodyScope = scope.Push()
        args |> List.mapi (fun i x -> bodyScope.SetVar($"${i+1}", x)) |> ignore
        let results = body |> List.map (eval_internal bodyScope)
        results |> List.last
        
    | FuncCalled _ -> failwith "eval this funccall not supported"
     
    | Call (id, args) ->
        let callArgs = args |> List.map (eval_internal scope)

        let real_name =
            match scope.GetAlias id with
            | Some alias -> alias
            | None -> id
        
        match (scope, real_name) with
        | Function f -> eval_internal scope (FuncCalled (callArgs, Func f))
        | IsStdFunction -> eval_std_function (id, callArgs) |> Option.get
        | _ -> failwith $"Function {id} not found"

    | If (c, b, Some e) ->
        let condition =
            match eval_internal scope c with
            | Bool b -> b
            | _ -> failwith "If condition not boolean"
            
        if condition then
            eval_internal scope b
        else 
            eval_internal scope e

    | If (c, b, None) ->
        let condition =
            match eval_internal scope c with
            | Bool b -> b
            | _ -> failwith "If condition not boolean"
            
        if condition then
            eval_internal scope b
        else 
            Nop
            
    | While (c, b) ->
        let condition =
            match eval_internal scope c with
            | Bool b -> b
            | _ -> failwith "If condition not boolean"
            
        while condition do
            eval_internal scope b
        
        Nop
    
    | Func _ -> failwith "Func should not exist in this stage"
    | Map _ -> failwith "Map should not exist in this stage"
    | Nop -> Nop
    | _ -> failwith $"Eval_internal Unsupported %A{ast}"

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
        | None -> failwith $"%A{name} not defined" 
     
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
