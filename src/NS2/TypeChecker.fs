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

let rec typecheck_block (scope:Scope) list=
    let rec inner i =
        function
        | [] -> []
        | x::xs ->
            let typed = typecheck_internal scope x list i
            match typed with
            | [x;Contract] -> [x]       //swallow functionality
            | x -> x @ inner (i+1) xs
    inner 0 list

and typecheck_internal (scope:Scope) (tree:AST) (blockrest:AST list) (i:int) : AST list =
    match tree with
    | Root body ->
        let elements = typecheck_block scope body
        [Typed (Root elements, VoidType)]
        
    | Block body ->
        let block_scope = scope.Spawn()
        let elements = typecheck_block block_scope body
        let last_typ = List.last elements |> GetType
        [Typed (Block elements, last_typ)]
        
    | Int n -> [Typed (tree, IntType)]
    | String x -> [Typed (tree, StringType)]
    | Id name ->
        (*let real_name =
            match scope.GetAlias name with
            | Some alias -> alias
            | None -> name
            
        match (scope, real_name) with
        | StdFunction -> failwith "Id IsStdFunction" //Call (real_name, [])
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
        [Typed (tree, typ)]
    
    | Assign (Id id, Block body) ->
        let (b,t) = typecheck_internal scope (Block body) blockrest i |> List.head |> TypedToTuple
        scope.SetFunc(id, b)
        scope.SetType(id, FunctionType t )
        [Typed (Func (id, b), VoidType)]
       
    | Assign (Id id, Id other) ->
        match (scope, other) with
        | StdFunction _ ->
            scope.SetAlias(id, other)
            []
        | Function _ ->
            scope.SetAlias(id, other)
            []
        | Variable value ->
            scope.SetVar(id, value)
            let typ = scope.GetType other |> Option.get
            scope.SetType(id, typ)
            [Typed (Assign (Id id, Typed (Id other, typ)), VoidType)]
        | _ -> failwith $"Variable {other} does not exist"
        
    | Assign (Id id, body) ->
        let (b,t) = typecheck_internal scope body blockrest i |> List.head |> TypedToTuple
        scope.SetVar(id, b)
        scope.SetType(id, t)
        [Typed (Assign (Id id, Typed (b,t)), VoidType)]

    | Unop (op, right) ->
        let (rr,t) = typecheck_internal scope right blockrest i |> List.head |> TypedToTuple
        [Typed (Unop (op, Typed (rr,t)), t)]
        
    | Binop (left, op, right) ->
        let ll = typecheck_internal scope left blockrest i |> List.head
        let rr = typecheck_internal scope right blockrest i |> List.head
        
        let typ =
            match op with
            | "==" -> BoolType
            | "!=" -> BoolType
            | "<" -> BoolType
            | "<=" -> BoolType
            | ">" -> BoolType
            | ">=" -> BoolType
            | _ -> GetType ll
        
        [Typed (Binop (ll, op, rr), typ)]

    //TODO int array, any array
    | Array elements ->
        let typed = elements |> List.collect (fun x -> typecheck_internal scope x blockrest i)
        [Typed (Array typed, ArrayType)]
        
    //| Pipe elements -> elements |> List.map (typecheck_internal scope) |> TypedPipe
    | Index (arr, idx) ->
        let tarr = typecheck_internal scope arr blockrest i  |> List.head
        let i = typecheck_internal scope idx blockrest i |> List.head |> GetNode
        [Typed (Index (tarr, i), GetType tarr)]

    (*| FuncCalled (args, fbody) ->
        let targs = args |> List.map (typecheck_internal scope) |> List.map fst
        let tbody = typecheck_internal scope fbody
        FuncCalled (targs, tbody)*)
            
    | Call (id, args) ->
        let targs = args |> List.map (fun x -> typecheck_internal scope x blockrest i |> List.head)
        let type_of_args = targs |> List.map GetType

        let real_name =
            match scope.GetAlias id with
            | Some alias -> alias
            | None -> id
            
        match (scope, id) with
        | StdFunction ret_typ ->
            [Typed (Call(translate_std_function id type_of_args, targs), ret_typ)]
        | Function _ ->
            let typ =
                match scope.GetType real_name with
                | Some (FunctionType t) -> t
                | _ -> failwith $"No type known for {id}"
            [Typed (Call(id,targs), typ)]
        | _ -> failwith $"Function {id} not found"
        
    //TODO dont change variables in c
    | If (c, b, Some e) ->
        let cc = typecheck_internal scope c blockrest i |> List.head
        let bb = typecheck_internal scope (Helpers.to_block b) blockrest i |> List.head
        let ee = typecheck_internal scope (Helpers.to_block e) blockrest i |> List.head
        
        let then_assigns = Helpers.find_assigns scope bb
        let else_assigns = Helpers.find_assigns scope ee
        
        let thenset = then_assigns.Keys |> Set.ofSeq
        let elseset = else_assigns.Keys |> Set.ofSeq
        
        let phis = HashSet<AST>()
        let mutable swallow = false
        for var in Set.union thenset elseset do
            if thenset.Contains var && elseset.Contains var then
                //assign in both branches
                let then_ty = then_assigns[var]
                let else_ty = else_assigns[var]
                let ty = scope.GetType var |> Option.get
                if then_ty = else_ty && then_ty = ty then
                    phis.Add (Typed(Phi (var,var,var), ty)) |> ignore
                else
                    swallow <- true
            else
                //assign in 1 branch
                let ty = scope.GetType var |> Option.get
                if then_assigns.ContainsKey var && then_assigns[var] = ty  then 
                    phis.Add (Typed(PhiSingle (var,var,null), ty)) |> ignore
                if then_assigns.ContainsKey var && then_assigns[var] <> ty  then 
                    swallow <- true
                    
                if else_assigns.ContainsKey var && else_assigns[var] = ty  then 
                    phis.Add (Typed(PhiSingle (var,null,var), ty)) |> ignore
                if else_assigns.ContainsKey var && else_assigns[var] <> ty  then 
                    swallow <- true
                
        if swallow then
            let remaining = blockrest[i+1..]
            
            let bbb = typecheck_internal scope (Helpers.extend_block b remaining) blockrest i |> List.head
            let eee = typecheck_internal scope (Helpers.extend_block e remaining) blockrest i |> List.head
            
            let ret = Typed (IfPhi(cc, bbb, Some eee, []), VoidType)
            [ret; Contract]
            
        else
            //TODO compute final type
            [Typed (IfPhi(cc, bb, Some ee, List.ofSeq phis), VoidType)]
        
    //TODO dont change variables in c
    //TODO return type
    | If (c, b, None) ->
        let cc = typecheck_internal scope c blockrest i |> List.head
        let bb = typecheck_internal scope (Helpers.to_block b) blockrest i |> List.head
        let then_assigns = Helpers.find_assigns scope bb
        
        let phis = HashSet<AST>()
        let mutable swallow = false
        for var in then_assigns.Keys do
            let then_ty = then_assigns[var]
            match scope.GetType var with
            | None -> failwith "if-should not be possible"
            | Some ty ->
                if ty = then_ty then
                    phis.Add (Typed(PhiSingle (var,var,null), then_ty)) |> ignore
                else
                    swallow <- true

        if swallow then
            let remaining = blockrest[i+1..]
            
            let bbb = typecheck_internal scope (Helpers.extend_block b remaining) blockrest i |> List.head
            let eee = typecheck_internal scope (Block remaining) blockrest i |> List.head
            
            let ret = Typed (IfPhi(cc, bbb, Some eee, []), VoidType)
            [ret; Contract]
        else
            [Typed (IfPhi(cc, bb, None, List.ofSeq phis), VoidType)]
        
    | While (c, b) ->
        let cc = typecheck_internal scope c blockrest i |> List.head
        let bb = typecheck_internal scope (Helpers.to_block b) blockrest i |> List.head
        
        let cond_vars = Helpers.find_usage scope cc
        let assigns = Helpers.find_assigns scope bb
        
        let condPhi, bodyPhi = assigns.Keys |> List.ofSeq |> List.partition cond_vars.Contains
        let cond_phi = condPhi |> List.map (fun var -> Typed(PhiSingle(var,null,null), assigns[var]))
        let body_phi = bodyPhi |> List.map (fun var -> Typed(PhiSingle(var,null,null), assigns[var]))
        
        [Typed (WhilePhi(cond_phi, cc, bb, body_phi), VoidType)]
        
    //| Map ( Array a , Func f) -> failwith "not implemented" // a |> List.map (fun x -> FuncCalled ([x], Func f)) |> TypedArray
    //| Map (arr, func) -> failwith "Can only map an array with a function"
    | Nop -> []
    | _ -> failwith $"typecheck_internal unsupported %A{tree}"
    
let typecheck (tree:AST) =
    let scope = Scope.Empty
    typecheck_internal scope tree [] 0 |> List.head