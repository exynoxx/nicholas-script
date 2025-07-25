module NS2.Helpers

open System.Collections.Generic
open NS2.Ast
open NS2.Scope
open NS2.Type


let to_block ast= 
    match ast with
    | Block _ -> ast
    | x -> (Block [x])

let extend_block block elems = 
    match block with
    | Block body -> Block (body @ elems)
    | x -> Block (x::elems)

let rec find_assigns (scope:Scope) ast =
    let set = Dictionary<string, Type>()
    
    let rec inner = 
        function
        | Block body ->
            for x in body do
                inner x
        | Assign (Id id, Block body) -> ()
        | Assign (Id id, Typed (_,t)) ->
            if scope.GetVariable id <> None then
                set[id] <- t
        | If (c, b, Some e) ->
            inner b
            inner e
        | If (c, b, None) -> inner b
        | While (c, b) -> inner b
        | Typed (x,t) -> inner x
        | Int n -> ()
        | String x -> ()
        | Id name -> ()
        | Unop (op, right) -> ()
        | Binop (left, op, right) -> ()
        | Array elements -> ()
        | Index (arr, idx) -> ()
        | Call (id, args) -> ()
        | Nop -> ()
        | x -> failwith $"find_assigns unsupported %A{x}"
        
    inner ast
    set
    
let rec find_usage (scope:Scope) ast =
    let set = HashSet<string>()
    
    let rec inner = 
        function
        | Block body ->
            for x in body do
                inner x
        | Assign (Id id, x) -> inner x
        | If (c, b, Some e) ->
            inner b
            inner e
        | If (c, b, None) -> inner b
        | While (c, b) -> inner b
        | Typed (x,t) -> inner x
        | Int n -> ()
        | String x -> ()
        | Id name -> set.Add name |> ignore
        | Unop (op, right) -> inner right
        | Binop (left, op, right) ->
            inner left
            inner right
        | Array elements ->
            for x in elements do
                inner x
        | Index (arr, idx) -> ()
        | Call (id, args) -> ()
        | Nop -> ()
        | x -> failwith $"find_assigns unsupported %A{x}"
        
    inner ast
    set