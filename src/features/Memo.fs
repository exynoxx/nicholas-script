/// `memo f a = …` / `f = memo:{…}`: each function instance first looks its arguments up in its own global cache
/// (a map created at program start), and stores what it computes.
module Features.Memo

open System.Collections.Generic
open Ast
open Core
open Typer

/// Blocks wrapped in `memo:`, and the cache variable of every memoized instance.
let private memos = HashSet<int>()
let private caches = ResizeArray<string>()

let private memoize (id: int) (arity: int) (body: S list) =
    if not (memos.Contains id) then body else
    match List.rev body with
    | Do value :: rest ->
        let cache, key, r = Desugar.fresh "memo", Desugar.fresh "k", Desugar.fresh "r"
        caches.Add(declare global0 cache (TMap(newHole (), newHole ())))
        let v n = mk (Var n)
        let args = List.init arity (fun i -> v $"_{i + 1}")
        let k = match args with [ a ] -> a | [] -> mk (Int 0L) | _ -> mk (Call("tuple", args))
        let miss = mk (Seq(List.rev rest @ [ Assign(r, value); SetIdx(v cache, v key, v r) ], v r))
        [ Assign(key, k); Do(mk (If(mk (Call("has", [ v cache; v key ])), mk (Index(v cache, v key)), miss))) ]
    | _ -> body

/// Every cache, created empty before main.
let private create () =
    caches |> Seq.map (fun u ->
        match resolve varTy.[u] with
        | TMap(k, v) as t -> Assign(u, typed (Call("mapof", [ typed (Arr []) (TArr k); typed (Arr []) (TArr v) ])) t)
        | t -> err $"memo cache of type {showT t}") |> List.ofSeq

let feature =
    { Feature.empty with
        name = "memo"
        builtins = [ "memo" ]
        modifiers = [ "memo" ]
        fnStarts = [ ID "memo" ]
        prims =
          [ "memo", function
                | [ { k = Fn(id, _) } as f ] -> (memos.Add id |> ignore; Some f)
                | _ -> err "memo: expects a block, as in f = memo:{...}" ]
        bodyRewrite = [ memoize ]
        noSpread = [ memos.Contains ] // a cache keyed by one tuple argument
        prologue = [ create ]
        reset = [ fun () -> memos.Clear(); caches.Clear() ] }
