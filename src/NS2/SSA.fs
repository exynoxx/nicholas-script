module NS2.SSA

open System.Collections.Generic
open NS2.Ast

type SSA_Scope (parent:SSA_Scope option, version_counter:Dictionary<string, int>) =
    
    let local_version = Dictionary<string, int>()
    
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
        let current = this.Version id
        let next = current+1
        version_counter[id] <- next
        local_version[id] <- next

        match next with
        | 0 -> id
        | _ -> $"{id}_{next}"

    static member Empty() = SSA_Scope(None, Dictionary<string,int>())
    member this.Spawn() = SSA_Scope(Some this, version_counter)
    

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
        | If (c, b, e) -> If(transform scope c, transform scope b, Option.map (transform scope) e)
        | IfPhi (c, b, e, phis) -> IfPhi(transform scope c, transform scope b, Option.map (transform scope) e, phis |> List.map (transform scope))
        | Phi (var, _, _) ->
            let latest = scope.Version var
            let newName = scope.NewId var
            Phi (newName, $"{var}_{latest-1}", $"{var}_{latest}")
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
