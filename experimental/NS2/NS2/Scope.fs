module NS2.Scope

open System.Collections.Generic
open NS2.Ast


type Scope (parent: Scope option) =
    
    let vars = Dictionary<string, AST>()
    let funcs = Dictionary<string, AST list>()
    let alias = Dictionary<string, string>()

    member this.GetVariable(name: string) : AST option =
        match vars.ContainsKey name with
        | true -> Some vars[name]
        | _ ->
            match parent with
            | Some p -> p.GetVariable(name)
            | None -> None
            
    member this.GetFunction(name: string) : AST list option =
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

    member this.SetVar(name: string, value: AST)= vars[name] <- value
    member this.SetFunc(name: string, value: AST list)= funcs[name] <- value
    member this.SetAlias(name: string, value: string)= alias[name] <- value
    member this.Push() : Scope = Scope(Some this)
    static member Empty = Scope(None)

