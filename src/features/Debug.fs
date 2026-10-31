/// `--debug`: after every assignment to a user variable, `println:"line: x = "+x` (a core pass).
module Features.Debug

open Ast
open Core
open Typer

/// Debug mode: after every assignment to a user variable, `println:"line: x = "+x`.
let rec private debugE (e: E) : E =
    let d = debugE
    let k =
        match e.k with
        | Arr xs -> Arr(List.map d xs)
        | Bin(op, a, b) -> Bin(op, d a, d b)
        | If(c, a, b) -> If(d c, d a, d b)
        | Call(f, args) -> Call(f, List.map d args)
        | Index(x, i) -> Index(d x, d i)
        | Iter it -> Iter { it with src = d it.src; body = debugSS it.body }
        | Fn(id, body) -> Fn(id, debugSS body)
        | Conv x -> Conv(d x)
        | Seq(ss, x) -> Seq(debugSS ss, d x)
        | k -> k
    { e with k = k }

and private debugSS (ss: S list) : S list =
    // a block's parameter copies come before its first line marker
    let mutable line = ss |> List.tryPick (function Line n -> Some n | _ -> None) |> Option.defaultValue 0
    // a named parameter `n` is renamed `n'<block id>`
    let user (n: string) = match n.IndexOf '\'' with -1 -> n | k -> n.Substring(0, k)
    // the typer drops it for a block (`f = memo:{..}` too)
    let show n = [ Do(mk (Call("debug", [ mk (Str $"{showLine line}: {user n} = "); mk (Var n) ]))) ]
    ss |> List.collect (fun s ->
        match s with
        | Line n -> (line <- n; [ s ])
        | Assign(n, e) -> Assign(n, debugE e) :: (if n.Contains '%' || n.StartsWith "prev'^" then [] else show n)
        | SetIdx(x, i, v) -> SetIdx(debugE x, debugE i, debugE v) :: (match x.k with Var n when not (n.Contains '%') -> show n | _ -> [])
        | While(c, body) -> [ While(debugE c, debugSS body) ]
        | Do e -> [ Do(debugE e) ])

let feature =
    { Feature.empty with
        name = "debug"
        corePasses = [ 0, "debug prints", fun ss -> if Desugar.debug then debugSS ss else ss ]
        // --debug of a block variable: typing the bare `f` would call it
        intercept =
          [ fun e ->
                match e.k with
                | Call("debug", [ _; { k = Var n } ]) when (match lookup n with Some u -> (match resolve varTy.[u] with TFn _ -> true | _ -> false) | None -> false) ->
                    Some(typed (Int 0L) TInt)
                | _ -> None ]
        prims =
          [ "debug", function
                | [ label; x ] ->
                    match resolve x.t with
                    | TFn _ -> Some(typed (Int 0L) TInt)
                    | _ -> Some(builtin "println" [ typeE (mk (Bin("+", label, x))) ])
                | _ -> None ] }
