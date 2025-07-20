module NS2.TypeChecker

open NS2.Ast
open NS2.Scope
open NS2.Type

let GetType =
    function
    | Typed(_,t) -> t
    | _ -> failwith "GetType not called with Typed"

let GetNode =
    function
    | Typed(x,_) -> x
    | _ -> failwith "GetNode not called with Typed"

let TypedToTuple =
    function
    | Typed(a,b) -> (a,b)
    | _ -> failwith "GetNode not called with Typed"


let rec typecheck_internal (scope:Scope) (tree:AST) : AST =
    match tree with
    | Root body ->
        let root = body |> List.map (typecheck_internal scope)
        Typed (Root root, AnyType)
        
    | Block body ->
        let typed = body |> List.map (typecheck_internal scope)
        let last_typ = List.last typed |> GetType
        Typed (Block typed, last_typ)
        
    | Int n -> Typed (tree, IntType)
    | String x -> Typed (tree, StringType)
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
        Typed (tree, typ)
    
    | Assign (Id id, Block body) ->
        let (b,t) = typecheck_internal scope (Block body) |> TypedToTuple
        scope.SetFunc(id, b)
        scope.SetType(id, FunctionType t )
        Typed (Func (id, b), VoidType)
       
    | Assign (Id id, Id other) ->
        
        match (scope, other) with
        | IsStdFunction -> ()
        | Function _ -> scope.SetAlias(id, other)
        | Variable value -> scope.SetVar(id, value)
        | _ -> failwith $"Variable {other} does not exist"
        
        Typed (tree, VoidType)
        
    | Assign (Id id, body) ->
        let (b,t) = typecheck_internal scope body |> TypedToTuple
        scope.SetVar(id, b)
        scope.SetType(id, t)
        Typed (Assign (Id id, Typed (b,t)), VoidType)

    | Unop (op, right) ->
        let (rr,t) = typecheck_internal scope right |> TypedToTuple
        Typed (Unop (op, Typed (rr,t)), t)
        
    | Binop (left, op, right) ->
        let ll = typecheck_internal scope left
        let rr = typecheck_internal scope right
        //TODO op is std or custom that exists
        Typed (Binop (ll, op, rr), GetType ll)

    //TODO int array, any array
    | Array elements ->
        let typed = elements |> List.map (typecheck_internal scope)
        Typed (Array typed, ArrayType)
        
    //| Pipe elements -> elements |> List.map (typecheck_internal scope) |> TypedPipe
    | Index (arr, idx) ->
        let tarr = typecheck_internal scope arr
        let i = typecheck_internal scope idx |> GetNode
        Typed (Index (tarr, i), GetType tarr)

    (*| FuncCalled (args, fbody) ->
        let targs = args |> List.map (typecheck_internal scope) |> List.map fst
        let tbody = typecheck_internal scope fbody
        FuncCalled (targs, tbody)*)
            
    | Call (id, args) ->
        let targs = args |> List.map (typecheck_internal scope)

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
        Typed (Call(id,targs), typ)
        
    | If (c, b, Some e) ->
        let cc = typecheck_internal scope c
        let bb = typecheck_internal scope b
        let ee = typecheck_internal scope e
        //TODO compute final type
        Typed (If(cc, bb, Some ee), AnyType)
        
    | If (c, b, None) ->
        let cc = typecheck_internal scope c
        let bb = typecheck_internal scope b
        Typed (If(cc, bb, None), AnyType)
        
    | While (c, b) ->
        let cc = typecheck_internal scope c
        let bb = typecheck_internal scope b
        
        Typed (While(cc,bb), VoidType)
    //| Map ( Array a , Func f) -> failwith "not implemented" // a |> List.map (fun x -> FuncCalled ([x], Func f)) |> TypedArray
    //| Map (arr, func) -> failwith "Can only map an array with a function"
    | Nop -> Typed (Nop, VoidType)
    
    | _ -> failwith $"typecheck_internal unsupported %A{tree}"
    
let typecheck (tree:AST) =
    let scope = Scope.Empty
    typecheck_internal scope tree