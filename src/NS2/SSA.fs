module NS2.SSA

open System.Collections.Generic
open NS2.Ast

type SSA_Scope (parent:SSA_Scope option, local_version: Dictionary<string,int>, global_version:Dictionary<string, int>) =
    
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
    
    member this.GetIdGlobal (id:string) : string =
        match global_version.GetValueOrDefault(id,0) with
        | 0 -> id
        | version -> $"{id}_{version}"
    
    member this.NewId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1
        global_version[id] <- next
        local_version[id] <- next

        match next with
        | 0 -> id
        | _ -> $"{id}_{next}"

    static member Empty() = SSA_Scope(None, Dictionary<string,int>(), Dictionary<string,int>())
    member this.Spawn() = SSA_Scope(Some this, Dictionary<string,int>(), global_version)
    
    member this.Copy() = SSA_Scope(parent, local_version |> Dictionary, global_version |> Dictionary)
    

let ssa_transform (tree: AST) =
    let rec transform (scope:SSA_Scope) ast =
        match ast with
        | Root body ->
            let scope = SSA_Scope.Empty()
            body |> List.map (transform scope) |> Root
        | Block b ->
            let bock_scope = scope.Spawn()
            b |> List.map (transform bock_scope) |> Block
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
            let bb = transform scope b
            let bb_scope = scope.Copy()
            let ee = transform scope e
            let ee_scope = scope.Copy()
            
            let merge = function
                        | Typed(Phi(var,_,_), phi_typ) ->
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetIdGlobal var, ee_scope.GetIdGlobal var), phi_typ)
                        | Typed(PhiSingle(_,var,null), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetIdGlobal var, lastVar), phi_typ)
                            
                        | Typed(PhiSingle(_,null,var), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, lastVar, ee_scope.GetIdGlobal var), phi_typ)
            
            let pp = phis |> List.map merge
            
            IfPhi(cc,bb,Some ee,pp)
            
        | IfPhi (c, b, None, phis) ->
            let cc = transform scope c
            let bb = transform scope b
            let bb_scope = scope.Copy()
            
            let merge = function
                        | Typed(PhiSingle(_,var,null), phi_typ) ->
                            let lastVar = scope.GetId var
                            let newName = scope.NewId var
                            Typed(Phi(newName, bb_scope.GetIdGlobal var, lastVar), phi_typ)
                        | _ -> failwith "single if single assign not possible"

            let pp = phis |> List.map merge
            IfPhi(cc,bb,None,pp)

        | While (c, Typed(Block body, _)) ->
            
            let body_scope = scope.Spawn()
            let bb = body |> List.map (transform body_scope) |> Block
            let cc = transform body_scope c
            
            While(cc,bb)
        | Unop (op, r) -> Unop(op, transform scope r)
        | Array elements -> elements |> List.map (transform scope) |> Array 
        | Pipe elements -> elements |> List.map (transform scope) |> Pipe
        | Call (id, args) -> Call(scope.GetId id, args |> List.map (transform scope))
        | Index (arr, idx) -> Index (transform scope arr, transform scope idx)
        | Typed (x, t) -> Typed (transform scope x, t)
        (*| Map (Array a, Func f) ->
            let na = a |> List.map (transform scope) |> Array
            let nf = transform scope (Func f)
            Map (na,nf)*)
        | Int _ | String _ | Nop -> ast
        | x -> failwith $"SSA unknown %A{x}"

    transform (SSA_Scope.Empty()) tree
