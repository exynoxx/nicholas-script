/// `lazy f x = …`: a function whose result streams. A consumer that stops early (`.take:k`, `@k`, `.any`, `.all`,
/// `@?y`) passes the function a sink block, and the function hands each element of its result to the sink until
/// the sink says stop, so no whole result is built (and an infinite generator terminates). Other uses call it as a
/// plain function. Streams into Fusion's chains through `Fusion.streamSources`.
module Features.Lazy

open System.Collections.Generic
open Ast
open Core
open Typer
open Features

/// `lazy` functions (block ids), and the sink-passing version of each (see `sinkBody`) per argument count.
let private lazyFns = HashSet<int>()
let private lazySinks = Dictionary<int * int, int>()

/// `f:args` with f a `lazy` function: Some(block id, f, args).
let lazyCall (e: E) =
    match e.k with
    | Call(name, args) when not (name.StartsWith nameMark || name.StartsWith negMark) ->
        let f = if name.StartsWith callMark then name.Substring 1 else name
        match lookup f |> Option.map (fun u -> resolve varTy.[u]) with
        | Some(TFn id) when lazyFns.Contains id -> Some(id, f, args)
        | _ -> None
    | _ -> None

/// The source of an untyped map/filter chain (e itself when it is none).
let rec private chainSrc (e: E) =
    match e.k with
    | Iter it when e.t = TUnk && (it.kind = IMap || it.kind = IFilter) -> chainSrc it.src
    | _ -> e

/// Is e a `lazy` call, or a map/filter chain fed by one?
let private streams (e: E) = e.t = TUnk && (lazyCall (chainSrc e)).IsSome

/// e's chain source replaced by h.
let rec private withChainSrc (e: E) (h: E) =
    match e.k with
    | Iter it when e.t = TUnk && (it.kind = IMap || it.kind = IFilter) -> { e with k = Iter { it with src = withChainSrc it.src h } }
    | _ -> h

let private newIter kind src body =
    let el = Desugar.fresh "_"
    { kind = kind; src = src; el = el; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = body (mk (Var el)); post = [] }

/// The body of a `lazy` function rewritten to hand each element of its result to the block `sink` (a parameter),
/// stopping once the sink returns F; the statements before the result run first. It returns whether the sink
/// still wants more. Result forms: `a ++ b`, `c ? a : b`, `(…; x)`, `[x, y]`, `.flatten` of a map (its body's value
/// streams per element); anything else becomes `%yield` (typeAt), decided by its type.
let private sinkBody (body: S list) (sink: string) : S list =
    let m = Desugar.fresh "more"
    let v n = mk (Var n)
    let zero = mk (Int 0L)
    let guard ss = Do(mk (If(v m, mk (Seq(ss, zero)), zero)))
    let send x = Assign(m, mk (Call(sink, [ x ])))
    let rec emit (e: E) : S list =
        match e.k with
        | Bin("++", a, b) -> emit a @ [ guard (emit b) ]
        | If(c, a, b) -> [ Do(mk (If(c, mk (Seq(emit a, zero)), mk (Seq(emit b, zero))))) ]
        | Seq(ss, x) -> ss @ emit x
        | Arr xs -> xs |> List.mapi (fun i x -> if i = 0 then send x else guard [ send x ])
        | Call("flatten", [ { k = Iter it } as x ]) when x.t = TUnk && it.kind = IMap ->
            match List.rev it.body with
            | Do last :: rest -> [ Do(mk (Iter { it with kind = IEach; body = Do(mk (Call("%while", [ v m ]))) :: List.rev rest @ emit last })) ]
            | _ -> [ Do(mk (Call("%yield", [ e; v m; v sink ]))) ]
        | _ -> [ Do(mk (Call("%yield", [ e; v m; v sink ]))) ]
    match List.rev body with
    | Do last :: rest -> List.rev rest @ [ Assign(m, mk (Bool true)) ] @ emit last @ [ Do(v m) ]
    | _ -> body

/// x `streams`: x with its `lazy` call's arguments typed (typing it again reuses them), and whether the call gives an array.
let rec private lazyArgs (x: E) =
    let h = chainSrc x
    match lazyCall h with
    | Some(_, f, args) ->
        let call = { h with k = Call(f, args |> List.map (typeE >> own)) }
        withChainSrc x call, (match resolve (typeE call).t with TArr _ -> true | _ -> false)
    | None -> x, false

/// The sink-passing version of `lazy` function `id` called with n arguments: one more parameter, the sink block.
and private lazySink id n =
    match lazySinks.TryGetValue((id, n)) with
    | true, s -> s
    | _ ->
        let body, fr = fnDefs.[id]
        let s = Desugar.freshId ()
        lazySinks.[(id, n)] <- s
        fnDefs.[s] <- (sinkBody body $"_{n + 1}", fr)
        s

let private notIter (x: E) = match x.k with Iter _ -> false | _ -> true

/// `chain` stage over x, or x typed plainly when its lazy call gives no array.
let private stage x (onArr: E -> E) (plain: E -> E) =
    match lazyArgs x with
    | x, true -> onArr x
    | x, false -> plain x

let private intercept (e: E) : E option =
    match e.k with
    // a `lazy` call feeding an ending that stops early streams into it (typeIter); as a plain map stage if needed
    | Call("take", [ x; k ]) when notIter x && (lookup "take").IsNone && streams x ->
        Some(stage x (fun x -> Fusion.typeIter (newIter IMap x (fun el -> [ Do el ])) (Some k) false None) (fun x -> builtin "take" [ typeE x; typeE k ]))
    | Index(x, ({ k = (Int _ | Var _) } as i)) when notIter x && streams x && (match i.k with Int n -> n >= 0L | _ -> true) ->
        Some(stage x (fun x -> Fusion.typeIter (newIter IMap x (fun el -> [ Do el ])) None false (Some i)) (fun x -> typeIndex (typeE x) (typeE i)))
    // `.any` is `{bool:_}/|`, `.all` is `{bool:_}/&`, `@?y` is `{_ = y}/|`: a fold of bools stops at the first T / F
    | Call(("any" | "all") as f, [ x ]) when (lookup f).IsNone && streams x ->
        Some(stage x (fun x ->
            let fold = newIter IFold (mk (Iter(newIter IMap x (fun el -> [ Do(mk (Call("bool", [ el ]))) ])))) (fun _ -> [])
            typeE (mk (Iter { fold with body = [ Do(mk (Bin((if f = "any" then "|" else "&"), mk (Var fold.acc), mk (Var fold.el)))) ] })))
                (fun x -> builtin f [ typeE x ]))
    | Call("has", [ x; y ]) when streams x ->
        Some(stage x (fun x ->
            let t = Desugar.fresh "t"
            let fold = newIter IFold (mk (Iter(newIter IMap x (fun el -> [ Do(mk (Bin("=", el, mk (Var t)))) ])))) (fun _ -> [])
            typeE (mk (Seq([ Assign(t, y) ], mk (Iter { fold with body = [ Do(mk (Bin("|", mk (Var fold.acc), mk (Var fold.el)))) ] })))))
                (fun x -> builtin "has" [ typeE x; typeE y ]))
    | Call("%yield", [ e; { k = Var m }; { k = Var sink } ]) ->
        // one part of a `lazy` result (sinkBody): a lazy call passes the sink on; an array's elements go to the sink
        // one at a time (a map/filter chain over an array or int fuses into that loop); anything else is one element
        let send (x: E) = Assign(m, mk (Call(sink, [ x ])))
        match lazyCall e with
        | Some(id, _, args) -> Some(typeE (mk (Seq([ Assign(m, mk (Call("%lazy", mk (Int(int64 id)) :: args @ [ mk (Var sink) ]))) ], mk (Int 0L)))))
        | None ->
            let h = chainSrc e
            let th = typeE h
            let fuse = not (obj.ReferenceEquals(e, h)) && (match resolve th.t with TArr _ | TInt -> true | _ -> false)
            let e = if fuse then withChainSrc e th else typeE (withChainSrc e th)
            match fuse, resolve e.t with
            | true, _ | _, TArr _ ->
                Some(typeE (mk (Iter(newIter IEach e (fun el -> [ Do(mk (Call("%while", [ mk (Var m) ]))); send el ])))))
            | _ -> Some(typeE (mk (Seq([ send e ], mk (Int 0L)))))
    | Call("%lazy", { k = Int id } :: args) -> Some(userCall (lazySink (int id) (args.Length - 1)) (List.map (typeE >> own) args))
    | Call("%foldmore", [ { k = Str op }; acc ]) ->
        // Lower.iter fed by a `lazy` call: a fold of bools wants more until the first T (`|`) / F (`&`)
        let acc = typeE acc
        match resolve acc.t with
        | TBool when op = "|" -> Some(typed (Call("not", [ acc ])) TBool)
        | TBool -> Some acc
        | _ -> Some(typed (Bool true) TBool)
    | _ -> None

/// A `lazy` call feeding a chain whose ending stops early streams into it: the stages run per element, no array.
let private source (src: E) (stops: bool) =
    match lazyCall src with
    | Some(id, f, args) when stops ->
        let args = args |> List.map (typeE >> own)
        let plain = typeE { src with k = Call(f, args) } // types the call, gives the result type
        let feed fn = mk (Call("%lazy", mk (Int(int64 id)) :: args @ [ fn ]))
        Some(plain, (match resolve plain.t with TArr _ -> Some feed | _ -> None))
    | _ -> None

let feature =
    { Feature.empty with
        name = "lazy"
        modifiers = [ "lazy" ]
        prims =
          [ "lazy", function
                | [ { k = Fn(id, _) } as f ] ->
                    if cur.inst.IsSome then err "lazy: only a top-level function can be lazy"
                    lazyFns.Add id |> ignore
                    Some f
                | _ -> None ]
        intercept = [ intercept ]
        // a lazy function's sink version is keyed by its sink, the block that fixes the consumer's result
        fixerExempt = [ fun id -> lazySinks.ContainsValue id ]
        reset = [ fun () -> lazyFns.Clear(); lazySinks.Clear() ]
        init = [ fun () -> Fusion.streamSources.Add source ] }
