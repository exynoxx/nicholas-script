module NS2.TypeChecker

open NS2.Ast
open NS2.Scope
open NS2.StdLib

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
        
        match scope.GetVariable real_name with
        | Some v -> v
        | None ->
            match lookup_std_function real_name with
            | true -> Call (real_name, [])
            | false -> 
                match scope.GetFunction real_name with
                | Some _ -> Call (real_name, [])
                | None -> failwith $"typecheck_internal Unbound identifier: {real_name}({name})"
    
    | Assign (Id id, Id other) ->
        scope.SetAlias(id, other)
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
        
        (*match (l, r) with //TODO
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
        | _ -> failwith "Binop types other than int not supported for now"*)
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

        match lookup_std_function id with
        | true -> ()
        | false ->
            match scope.GetFunction id with
            | Some _ -> ()
            | None -> failwith $"Function {id} not found"
        Call(id,targs)
        
    | Map (arr, func) ->
        match (arr, func) with
        | Array a , Func f ->
            let elements = a |> List.map (fun x -> FuncCalled ([x], f))
            Array elements
        | _ -> failwith "Can only map an array with a function"
        
    | Nop -> Nop
    
    | _ -> failwith $"typecheck_internal unsupported %A{tree}"
    
let typecheck (tree:AST) =
    let scope = Scope.Empty
    typecheck_internal scope tree