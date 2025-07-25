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

    member this.NewId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1
        global_version[id] <- next
        local_version[id] <- next

        match next with
        | 0 -> id
        | _ -> $"{id}_{next}"

    member this.NextId (id:string) : string =
        let next = global_version.GetValueOrDefault(id,0)+1

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

        | WhilePhi (condPhi, c, b, bodyPhi) ->
            
            (*
            cond:
                a_2 = [entry: a1, loop: a3]
                a_2 < ...
            loop:
                a_3 = a_2 + 1
                a_4 = a_3 ...
            *)
            
            //reserve a2
            //find last(a_4) in body run
            //compute phi
            
            let unwrap = function | Typed(PhiSingle(var,_,_), _) -> var | _ -> failwith "WhilePhi.Unwrap fail"
            let initial_vars = condPhi |> List.map unwrap
            
            let init_var_lookup = initial_vars |> List.map (fun var -> KeyValuePair(var,scope.GetId var)) |> Dictionary
            let cond_var_lookup = initial_vars |> List.map (fun var -> KeyValuePair(var,scope.NewId var)) |> Dictionary

            let bb_scope = scope.Spawn()
            let bb = transform bb_scope b
            
            let cond_phi = condPhi |> List.map (function
                                                | Typed(PhiSingle(var,null,null), phi_typ) ->
                                                    let newName = cond_var_lookup[var]
                                                    let entryVar = init_var_lookup[var]
                                                    let lastNameBody = bb_scope.GetId var
                                                    Typed(Phi(newName, lastNameBody, entryVar), phi_typ)
                                                | _ -> failwith "single while assign not possible")
            
            let cc = transform scope c
            let merge = function
                        | Typed(PhiSingle(var,null,null), phi_typ) ->
                            let entryVar = scope.GetId var
                            let newName = scope.NewId var
                            let varInBody = bb_scope.GetId var
                            Typed(Phi(newName, varInBody, entryVar), phi_typ)
                        | _ -> failwith "single while assign not possible"
            let body_phi = bodyPhi |> List.map merge
            
            WhilePhi(cond_phi, cc,bb,body_phi)
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
