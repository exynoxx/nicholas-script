module NS2.TypeChecker

open NS2.Ast
open NS2.Scope
open NS2.StdLib

let rec typecheck_internal (scope:Scope) (tree:AST) =
    match tree with
    | Root body -> body |> List.map (typecheck_internal scope) |> Root
    | Int n -> Int n
    | String x -> String x
    | Id name ->
        let real_name =
            match scope.GetAlias name with
            | Some alias -> alias
            | None -> name
            
        match (scope, real_name) with
        | IsStdFunction -> Call (real_name, [])
        | Function _ -> Call (real_name, [])
        | Variable v -> v
        | _ -> failwith $"typecheck_internal Unbound identifier: {real_name}({name})"
        
    
    | Assign (Id id, Id other) ->
        
        match (scope, other) with
        | IsStdFunction -> ()
        | Function _ -> scope.SetAlias(id,other)
        | Variable value -> scope.SetVar(id, value)
        | _ -> failwith $"Variable {other} does not exist"
        Nop
        
    | Assign (Id id, body) ->
        let tbody = typecheck_internal scope body
        match tbody with
        | Func tb -> 
            scope.SetFunc(id,tb)
            NamedFunc (id, tb)
        | _ ->
            scope.SetVar(id, tbody)
            Assign (Id id, tbody)

    | Unaryop (op, right) ->
        let r = typecheck_internal scope right
        Unaryop (op, r)
        
    | Binop (left, op, right) ->
        let l = typecheck_internal scope left
        let r = typecheck_internal scope right
        //TODO op is std or custom that exists
        Binop (l, op, r)

    | Array elements -> elements |> List.map (typecheck_internal scope) |> Array
    | Pipe elements -> elements |> List.map (typecheck_internal scope) |> Pipe

    | Index (arrExpr, indexExpr) ->
        let arr = typecheck_internal scope arrExpr
        let i = typecheck_internal scope indexExpr
        Index (arr, i)

    | Func fbody ->
        let nested = scope.Push()
        let tbody = fbody |> List.map (typecheck_internal nested)
        Func tbody
       
    | FuncCalled (args, fbody) ->
        let targs = args |> List.map (typecheck_internal scope)
        let nested = scope.Push()
        let tbody = fbody |> List.map (typecheck_internal nested)
        FuncCalled (targs, tbody)
            
    | Call (id, args) ->
        let targs = args |> List.map (typecheck_internal scope)

        match (scope, id) with
        | IsStdFunction -> ()
        | Function _ -> ()
        | _ -> failwith $"Function {id} not found"
        Call(id,targs)
        
    | Map ( Array a , Func f) -> a |> List.map (fun x -> FuncCalled ([x], f)) |> Array
    | Map (arr, func) -> failwith "Can only map an array with a function"
    | Nop -> Nop
    
    | _ -> failwith $"typecheck_internal unsupported %A{tree}"
    
let typecheck (tree:AST) =
    let scope = Scope.Empty
    typecheck_internal scope tree