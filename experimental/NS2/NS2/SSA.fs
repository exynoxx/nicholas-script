module NS2.SSA

open System.Collections.Generic
open NS2.Ast

type SSA_Scope (parent: SSA_Scope option) =
    let counter = Dictionary<string, int>()
    
    member this.Version (id:string) =
        match counter.TryGetValue(id) with
            | true, v -> v
            | false, _ ->
                match parent with
                | Some p -> p.Version id
                | None -> 0
                
    member this.GetId (id:string) : string =
        match this.Version id with
        | 0 -> id
        | 1 -> id
        | version -> $"{id}_{version}"
    
    member this.NewId (id:string) : string =
        let current = this.Version id
        let next = current+1
        counter[id] <- next

        match current with
        | 0 -> id
        | _ -> $"{id}_{next}"
    
    member this.Spawn() = SSA_Scope(Some this)
    static member Empty() = SSA_Scope(None)
    

let ssa_transform (tree: AST) =
    let rec transform (scope:SSA_Scope) ast =
        match ast with
        | Root body ->
            let scope = SSA_Scope.Empty()
            body |> List.map (transform scope) |> Root

        | Id name -> Id (scope.GetId name)
        
        | Assign (Id id, rhs) ->
            let trhs = transform scope rhs
            let newName = scope.NewId id
            Assign(Id newName, trhs)

        | Func fbody ->
            let scope = scope.Spawn()
            fbody |> List.map (transform scope) |> Func

        | FuncCalled (args, body) ->
            let targs = args |> List.map (transform scope)
            let scope = scope.Spawn()
            let tbody = body |> List.map (transform scope)
            FuncCalled(targs, tbody)

        | Binop (l, op, r) -> Binop(transform scope l, op, transform scope r)
        | Unaryop (op, r) -> Unaryop(op, transform scope r)
        | Array elements -> elements |> List.map (transform scope) |> Array 
        | Pipe elements -> elements |> List.map (transform scope) |> Pipe
        | Call (id, args) -> Call(scope.GetId id, args |> List.map (transform scope))
        | Index (arr, idx) -> Index (transform scope arr, transform scope idx)
        | Map (Array a, Func f) ->
            let na = a |> List.map (transform scope) |> Array
            let nf = f |> List.map (transform scope) |> Func
            Map (na,nf)
        | x -> x

    transform (SSA_Scope.Empty()) tree
