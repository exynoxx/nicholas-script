module NS2.TypeChecker

open NS2.Ast
open NS2.Scope
open NS2.Type

let rec typecheck_internal (scope:Scope) (tree:AST) : AST*Type =
    match tree with
    | Root body ->
        let root = body |> List.map (typecheck_internal scope) |> List.map fst
        (Root root, AnyType)
        
    | Block body ->
        let typed = body |> List.map (typecheck_internal scope)
        let last_typ = List.last typed |> snd
        let elements = typed |> List.map fst
        (Block elements, last_typ)
        
    | Int n -> (tree, IntType)
    | String x -> (tree, StringType)
    | Id name ->
        (*let real_name =
            match scope.GetAlias name with
            | Some alias -> alias
            | None -> name
            
        match (scope, real_name) with
        | IsStdFunction -> failwith "Id IsStdFunction" //Call (real_name, [])
        | Function _ -> failwith "Id Function" //Call (real_name, [])
        | Variable _ -> Id name
        | _ when name.StartsWith "$" -> Id name
        | _ -> failwith $"typecheck_internal Unbound identifier: {real_name}({name})"*)
        
        let real_name =
            match scope.GetAlias name with
            | Some alias -> alias
            | None -> name
            
        let typ =
            match scope.GetType real_name with
            | Some t -> t
            | None -> failwith $"No type known for {name}"
        (tree, typ)
    
    | Assign (Id id, Block body) ->
        let (b,t) = typecheck_internal scope (Block body)
        scope.SetFunc(id, b)
        scope.SetType(id, FunctionType t )
        (Func (id, b), VoidType)
       
    | Assign (Id id, Id other) ->
        
        match (scope, other) with
        | IsStdFunction -> ()
        | Function _ -> scope.SetAlias(id, other)
        | Variable value -> scope.SetVar(id, value)
        | _ -> failwith $"Variable {other} does not exist"
        
        (tree, VoidType)
        
    | Assign (Id id, body) ->
        let (b,t) = typecheck_internal scope body
        scope.SetVar(id, b)
        scope.SetType(id, t)
        (Assign (Id id, b), VoidType)

    | Unop (op, right) ->
        let (rr,t) = typecheck_internal scope right
        (Unop (op, rr), t)
        
    | Binop (left, op, right) ->
        let l,lt = typecheck_internal scope left
        let r,rt = typecheck_internal scope right
        //TODO op is std or custom that exists
        (Binop (l, op, r), lt) //TODO

    //TODO int array, any array
    | Array elements ->
        let typed = elements |> List.map (typecheck_internal scope) |> List.map fst
        (Array typed, ArrayType)
        
    //| Pipe elements -> elements |> List.map (typecheck_internal scope) |> TypedPipe
    
    | Index (arr, idx) ->
        let arr,t = typecheck_internal scope arr
        let i,_ = typecheck_internal scope idx
        (Index (arr, i), t)

    //| Func fbody -> TypedFunc (typecheck_internal scope fbody)
    (*| FuncCalled (args, fbody) ->
        let targs = args |> List.map (typecheck_internal scope) |> List.map fst
        let tbody = typecheck_internal scope fbody
        FuncCalled (targs, tbody)*)
            
    | Call (id, args) ->
        let targs = args |> List.map (typecheck_internal scope) |> List.map fst

        let real_name =
            match scope.GetAlias id with
            | Some alias -> alias
            | None -> id
            
        let typ =
            match scope.GetType real_name with
            | Some (FunctionType t) -> t
            | _ -> failwith $"No type known for {id}"
        
        match (scope, id) with
        | IsStdFunction -> ()
        | Function _ -> ()
        | _ -> failwith $"Function {id} not found"
        (Call(id,targs), typ)
        
    | If (c, b, Some e) ->
        let cc,ct = typecheck_internal scope c
        let bb,bt = typecheck_internal scope b
        let ee,et = typecheck_internal scope e
        (If(cc, bb, Some ee), AnyType)
        
    | If (c, b, None) ->
        let (cc,ct) = typecheck_internal scope c
        let bb,bt = typecheck_internal scope b
        (If(cc, bb, None), AnyType)
        
    | While (c, b) ->
        let cc,ct = typecheck_internal scope c
        let bb,bt = typecheck_internal scope b
        
        (While(cc,bb), VoidType)
    //| Map ( Array a , Func f) -> failwith "not implemented" // a |> List.map (fun x -> FuncCalled ([x], Func f)) |> TypedArray
    //| Map (arr, func) -> failwith "Can only map an array with a function"
    | Nop -> (Nop, VoidType)
    
    | _ -> failwith $"typecheck_internal unsupported %A{tree}"
    
let typecheck (tree:AST) =
    let scope = Scope.Empty
    typecheck_internal scope tree |> fst