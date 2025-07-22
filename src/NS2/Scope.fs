module NS2.Scope

open System.Collections.Generic
open NS2.Ast
open NS2.StdLib
open NS2.Type


type Scope (parent: Scope option) =
    
    let vars = Dictionary<string, AST>()
    let funcs = Dictionary<string, AST>()
    let alias = Dictionary<string, string>()
    let typ = Dictionary<string, Type>()
    
    member this.GetVariable(name: string) : AST option =
        match vars.ContainsKey name with
        | true -> Some vars[name]
        | _ ->
            match parent with
            | Some p -> p.GetVariable(name)
            | None -> None
            
    member this.GetScopeAssign () = vars.Keys |> Seq.map (fun k -> KeyValuePair(k,typ[k])) |> Dictionary
            
    member this.GetFunction(name: string) : AST option =
        match funcs.ContainsKey name with
        | true -> Some funcs[name]
        | _ ->
            match parent with
            | Some p -> p.GetFunction(name)
            | None -> None
            
    member this.GetAlias(name: string) : string option =
        match alias.ContainsKey name with
        | true -> Some alias[name]
        | _ ->
            match parent with
            | Some p -> p.GetAlias(name)
            | None -> None
    
    member this.GetType(name: string) : Type option =
        match typ.ContainsKey name with
        | true -> Some typ[name]
        | _ ->
            match parent with
            | Some p -> p.GetType(name)
            | None -> None
    
    member this.SetVar(name: string, value: AST)= vars[name] <- value
    member this.SetFunc(name: string, value: AST)= funcs[name] <- value
    member this.SetAlias(name: string, value: string)= alias[name] <- value
    member this.SetType(name: string, t: Type)= typ[name] <- t
    member this.Push() : Scope = Scope(Some this)
    static member Empty = Scope(None)

 let (|StdFunction|Function|Variable|Unknown|) (scope:Scope, id: string) =
    match lookup_std_function id with
    | Some t ->
        StdFunction t
    | None ->
        match scope.GetFunction id with
        | Some f -> Function f
        | None ->
            match scope.GetVariable id with
            | Some v -> Variable v
            | _ -> Unknown