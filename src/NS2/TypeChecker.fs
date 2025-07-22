module NS2.TypeChecker

open System.Collections.Generic
open System.Reflection.Metadata
open NS2.Ast
open NS2.Scope
open NS2.StdLib
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

let dictionary_diff (a:Dictionary<string,Type>) (b:Dictionary<string,Type>) =
    let keys1 = a.Keys |> Set.ofSeq
    let keys2 = b.Keys |> Set.ofSeq
    
    let diff = Set.union keys1 keys2 - Set.intersect keys1 keys2
    diff


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
        | IsStdFunction ->
            scope.SetAlias(id, other)
            Nop
        | Function _ ->
            scope.SetAlias(id, other)
            Nop
        | Variable value ->
            scope.SetVar(id, value)
            let typ = scope.GetType other |> Option.get
            scope.SetType(id, typ)
            Typed (Assign (Id id, Typed (Id other, typ)), VoidType)
        | _ -> failwith $"Variable {other} does not exist"
        
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
        
        let typ =
            match op with
            | "==" -> BoolType
            | "!=" -> BoolType
            | "<" -> BoolType
            | "<=" -> BoolType
            | ">" -> BoolType
            | ">=" -> BoolType
            | _ -> GetType ll
        
        Typed (Binop (ll, op, rr), typ)

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
        let type_of_args = args |> List.map (typecheck_internal scope) |> List.map GetType

        let real_name =
            match scope.GetAlias id with
            | Some alias -> alias
            | None -> id
            
        match (scope, id) with
        | StdFunction ret_typ ->
            Typed (Call(translate_std_function id type_of_args, targs), ret_typ)
        | Function _ ->
            let typ =
                match scope.GetType real_name with
                | Some (FunctionType t) -> t
                | _ -> failwith $"No type known for {id}"
            Typed (Call(id,targs), typ)
        | _ -> failwith $"Function {id} not found"
        
        
    | If (c, b, Some e) ->
        
        let to_block =
            function
            | Block body -> Block body
            | x -> Block [x]
        
        let process_block =
            function
            | Block block -> 
                let scope = scope.Push()
                let typed = block |> List.map (typecheck_internal scope)
                let last_typ = List.last typed |> GetType
                let assigns = scope.GetScopeAssign ()
                (typed, last_typ, assigns)
            | _ -> failwith "Not possible"
            
        let (then_elements, then_typ, then_assigns) = process_block (to_block b)
        let (else_elements, else_typ, else_assigns) = process_block (to_block e)
        
        let thenset = then_assigns.Keys |> Set.ofSeq
        let elseset = else_assigns.Keys |> Set.ofSeq
        
        let phis = HashSet<AST>() 
        for var in Set.union thenset elseset do
            if not (thenset.Contains var && elseset.Contains var) then
                //only one branch contains
                //default value
                ()
            else
                let then_ty = then_assigns[var]
                let else_ty = else_assigns[var]
                if then_ty <> else_ty then
                    failwith $"variable {var} has different types in different branches" //TODO StringType
                else
                    phis.Add (Typed(Phi (var,var,var), then_ty)) |> ignore
        
        let cc = typecheck_internal scope c
        //TODO compute final type
        Typed (IfPhi(cc, Typed (Block then_elements,VoidType), Some (Typed (Block else_elements,VoidType)), List.ofSeq phis), VoidType)
        
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