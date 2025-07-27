module NS2.SSA

open System
open System.Collections
open System.Collections.Generic
open NS2.Ast
open NS2.Type

type SSA_Scope (parent:SSA_Scope option, local_version: Dictionary<string,int>, global_version:Dictionary<string, int>, reverse:Dictionary<string, string>) =
    
    member this.Global_version = global_version
    
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
    member this.IsLastVersion(id:string) =
         let unversioned = reverse[id]
         id = $"{unversioned}_{this.Global_version[unversioned]}" 
    
    member this.NextId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1

        match next with
        | 0 -> id
        | _ -> $"{id}_{next}"
    
    static member FindModified(first: SSA_Scope, second:SSA_Scope) =
        first.Global_version.Keys
        |> Seq.where(fun var -> second.Global_version.ContainsKey var && first.Global_version[var] < second.Global_version[var])
        |> HashSet
        
    
    static member Empty() = SSA_Scope(None, Dictionary<string,int>(), Dictionary<string,int>(), Dictionary<string,string>())
    member this.Spawn() = SSA_Scope(Some this, Dictionary<string,int>(), global_version, reverse)
    
    member this.Copy() = SSA_Scope(parent, local_version |> Dictionary, global_version |> Dictionary, reverse |> Dictionary)
    
let rec replace_assigns (scope:SSA_Scope) (a_to_hoist:Dictionary<string,string>) ast =
    match ast with
    | Typed(Phi(var,x, y), typ) ->
        if scope.IsLastVersion var then
            let unversioned = scope.Reverse var
            let replacement = a_to_hoist[unversioned]
            [
                Typed(Phi(var,x, y), typ) 
                Typed(Store (Id replacement, Typed(Id var, typ)), VoidType)
            ]
        else
            [ast]

    | Assign (Id var, Typed(rhs,typ)) ->
        if scope.IsLastVersion var then
            let unversioned = scope.Reverse var
            let replacement = a_to_hoist[unversioned]
            [Store (Id replacement, Typed(rhs,typ))]
        else
            [ast]
            
    | Root body ->  [ body |> List.collect (replace_assigns scope a_to_hoist) |> Root ]
    | Block b -> [ b |> List.collect (replace_assigns scope a_to_hoist) |> Block ]
    | Id name -> [Id (scope.GetId name)]  
    | Binop (l, op, r) ->
        let ll = replace_assigns scope a_to_hoist l |> List.head
        let rr = replace_assigns scope a_to_hoist r |> List.head
        [Binop(ll, op, rr)]
    | Unop (op, r) ->
        let rr = replace_assigns scope a_to_hoist r |> List.head
        [Unop(op, rr)]
    | IfPhi (c, b, Some e, phis) ->
        let cc = replace_assigns scope a_to_hoist c |> List.head
        let bb = replace_assigns scope a_to_hoist b |> List.head
        let ee = replace_assigns scope a_to_hoist e |> List.head
        let pp = phis |> List.collect (replace_assigns scope a_to_hoist)
        [IfPhi(cc,bb,Some ee,pp)]
    | IfPhi (c, b, None, phis) ->
        let cc = replace_assigns scope a_to_hoist c |> List.head
        let bb = replace_assigns scope a_to_hoist b |> List.head
        let pp = phis |> List.collect (replace_assigns scope a_to_hoist)
        [IfPhi(cc,bb,None,pp)]
    | Array elements -> [elements |> List.collect (replace_assigns scope a_to_hoist) |> Array ]
    | Pipe elements -> [elements |> List.collect (replace_assigns scope a_to_hoist) |> Pipe]
    | Call (id, args) -> [Call(scope.GetId id, args |> List.collect (replace_assigns scope a_to_hoist))]
    | Index (arr, idx) -> [Index (replace_assigns scope a_to_hoist arr |> List.head, replace_assigns scope a_to_hoist idx |> List.head)]
    | Typed (x, t) -> replace_assigns scope a_to_hoist x |> List.map(fun xx -> Typed(xx,t))
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

        | WhileExtra (c, b, types) ->
            //test run
            //find variables assigned
            //reserve global first
            let before = scope.Copy()
            transform (scope.Spawn()) b
            let modified = SSA_Scope.FindModified(before,scope)
            let oldName = modified |> Seq.map (fun var -> KeyValuePair(var,scope.GetId var)) |> Dictionary
            let newName = modified |> Seq.map (fun var -> KeyValuePair(var,scope.NewId var)) |> Dictionary
            let pre_assigns = modified
                              |> Seq.collect (fun var ->
                                            [
                                                CreatePtr (Id newName[var], types[var])
                                                Store (Id newName[var], Typed(Id oldName[var], types[var]))
                                            ])
                              |> List.ofSeq
            
            let b_scope = scope.Spawn()
            let bb = transform b_scope b

            let bbb = replace_assigns scope newName bb |> List.head
            let cc = transform scope c
            
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
