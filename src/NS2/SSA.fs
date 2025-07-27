module NS2.SSA

open System
open System.Collections
open System.Collections.Generic
open NS2.Ast
open NS2.Type

type SSA_Scope (parent:SSA_Scope option, local_version: Dictionary<string,int>, global_version:Dictionary<string, int>, reverse:Dictionary<string, string>) =
    
    member this.Version (id:string) =
        match local_version.TryGetValue(id) with
            | true, v -> v
            | false, _ ->
                match parent with
                | Some p -> p.Version id
                | None -> 0
                
    member this.GetId (id:string) : string =
        match this.Version id with
        | 0 -> id
        | version -> $"{id}_{version}"

    member this.NewId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1
        global_version[id] <- next
        local_version[id] <- next

        let newId =
            match next with
            | 0 -> id
            | _ -> $"{id}_{next}"
            
        reverse[newId] <- id
        newId

    member this.Reverse(id:string) = reverse[id]
    
    (*
    member this.NextId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1

        match next with
        | 0 -> id
        | _ -> $"{id}_{next}"
        *)
    
    static member Empty() = SSA_Scope(None, Dictionary<string,int>(), Dictionary<string,int>(), Dictionary<string,string>())
    member this.Spawn() = SSA_Scope(Some this, Dictionary<string,int>(), global_version, reverse)
    
    member this.Copy() = SSA_Scope(parent, local_version |> Dictionary, global_version |> Dictionary, reverse |> Dictionary)
    
let rec replace_assigns (scope:SSA_Scope) (pre_assigns:ResizeArray<string*Type>) ast =
    match ast with
    | Root body ->  [ body |> List.collect (replace_assigns scope pre_assigns) |> Root ]
    | Block b -> [ b |> List.collect (replace_assigns scope pre_assigns) |> Block ]
    | Id name -> [Id (scope.GetId name)]  
    | Binop (l, op, r) ->
        let ll = replace_assigns scope pre_assigns l |> List.head
        let rr = replace_assigns scope pre_assigns r |> List.head
        [Binop(ll, op, rr)]
        
    | Unop (op, r) ->
        let rr = replace_assigns scope pre_assigns r |> List.head
        [Unop(op, rr)]

    | IfPhi (c, b, Some e, phis) ->
        let cc = replace_assigns scope pre_assigns c |> List.head
        let bb = replace_assigns scope pre_assigns b |> List.head
        let ee = replace_assigns scope pre_assigns e |> List.head
        let pp = phis |> List.collect (replace_assigns scope pre_assigns)
        [IfPhi(cc,bb,Some ee,pp)]
        
    | IfPhi (c, b, None, phis) ->
        let cc = replace_assigns scope pre_assigns c |> List.head
        let bb = replace_assigns scope pre_assigns b |> List.head
        let pp = phis |> List.collect (replace_assigns scope pre_assigns)
        [IfPhi(cc,bb,None,pp)]
        
    | Typed(Phi(var,x, y), typ) ->
        let unversioned = scope.Reverse var
        
        if var = $"{unversioned}_{scope.Version unversioned}" then 

            let tmp = scope.NewId unversioned
            pre_assigns.Add((tmp,typ))

            [
                Typed(Phi(var,x, y), typ) 
                Typed(Store (Id tmp, Id var), typ)
            ]
            
        else
            [ast]

    | Assign (Id id, Typed(rhs,typ)) ->
        
        let unversioned = scope.Reverse id
        if id = $"{unversioned}_{scope.Version unversioned}" then
            let tmp = scope.NewId id
            pre_assigns.Add((tmp,typ))
            [Store (Id tmp, Typed(rhs,typ))]
        else
            [ast]
            
    | Array elements -> [elements |> List.collect (replace_assigns scope pre_assigns) |> Array ]
    | Pipe elements -> [elements |> List.collect (replace_assigns scope pre_assigns) |> Pipe]
    | Call (id, args) -> [Call(scope.GetId id, args |> List.collect (replace_assigns scope pre_assigns))]
    | Index (arr, idx) -> [Index (replace_assigns scope pre_assigns arr |> List.head, replace_assigns scope pre_assigns idx |> List.head)]
    | Typed (x, t) -> replace_assigns scope pre_assigns x |> List.map(fun xx -> Typed(xx,t))
    | Int _ | String _ | Nop -> [ast]
    | x -> failwith $"SSA replace_assigns unknown %A{x}"

let ssa_transform (tree: AST) =
    let rec transform (scope:SSA_Scope) ast =
        match ast with
        | Root body ->
            let scope = SSA_Scope.Empty()
            body |> List.map (transform scope) |> Root
        | Block b ->
            b |> List.map (transform scope) |> Block
        | Id name -> Id (scope.GetId name)
        | Assign (Id id, rhs) ->
            let trhs = transform scope rhs
            let newName = scope.NewId id
            Assign(Id newName, trhs)
        | FuncCalled (args, body) ->
            let targs = args |> List.map (transform scope)
            let tbody = (transform scope body)
            FuncCalled(targs, tbody)
        | Binop (l, op, r) -> Binop(transform scope l, op, transform scope r)
        | IfPhi (c, b, Some e, phis) ->
            let cc = transform scope c
            let bb_scope = scope.Spawn()
            let bb = transform bb_scope b
            let ee_scope = scope.Spawn()
            let ee = transform ee_scope e
            
            let merge = function
                        | Typed(Phi(var,_,_), phi_typ) ->
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetId var, ee_scope.GetId var), phi_typ)
                        | Typed(PhiSingle(_,var,null), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetId var, lastVar), phi_typ)
                            
                        | Typed(PhiSingle(_,null,var), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, lastVar, ee_scope.GetId var), phi_typ)
            
            let pp = phis |> List.map merge
            
            IfPhi(cc,bb,Some ee,pp)
            
        | IfPhi (c, b, None, phis) ->
            let cc = transform scope c
            let bb_scope = scope.Spawn()
            let bb = transform bb_scope b
            
            let merge = function
                        | Typed(PhiSingle(var,null,null), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetId var, lastVar), phi_typ)
                        | _ -> failwith "single if single assign not possible"

            let pp = phis |> List.map merge
            IfPhi(cc,bb,None,pp)

        | While (c, b) ->
            let pre_assign_b = ResizeArray<string*Type>()
            let b_scope = scope.Spawn()
            let bb = transform b_scope b
            let bbb = replace_assigns b_scope pre_assign_b bb |> List.head
            
            let assignInstructions (tmp,typ) =
                let unversioned = scope.Reverse tmp
                let initial_var = scope.GetId unversioned
                                
                [
                    CreatePtr (Id tmp, typ)
                    Store (Id tmp, Typed(Id initial_var, typ))
                ]
            
            let pre_assigns = pre_assign_b |> Seq.collect assignInstructions |> List.ofSeq

            let cc = transform b_scope c
            
            WhilePhi(cc, bbb, pre_assigns)

        | Unop (op, r) -> Unop(op, transform scope r)
        | Array elements -> elements |> List.map (transform scope) |> Array 
        | Pipe elements -> elements |> List.map (transform scope) |> Pipe
        | Call (id, args) -> Call(scope.GetId id, args |> List.map (transform scope))
        | Index (arr, idx) -> Index (transform scope arr, transform scope idx)
        | Store (id, x) -> Store (id, transform scope x)
        | Typed (x, t) -> Typed (transform scope x, t)
        (*| Map (Array a, Func f) ->
            let na = a |> List.map (transform scope) |> Array
            let nf = transform scope (Func f)
            Map (na,nf)*)
        | Int _ | String _ | Nop -> ast
        | x -> failwith $"SSA unknown %A{x}"

    transform (SSA_Scope.Empty()) tree
