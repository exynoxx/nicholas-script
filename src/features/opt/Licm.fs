/// Loop-invariant code motion (LICM): a pure expression inside a `While` whose inputs the loop never writes is
/// computed once before it.
///
///   for each While (innermost first):
///     written  = every variable the loop assigns, pushes into or SetIdx's, plus what its callees write
///     hoist e  when  e is pure, reads nothing in `written`, has a hoistable type and contains a call
///
/// A *partial* expression (may panic or not terminate: index, `/`, recursion, user loops) is only hoisted under a
/// guard `if cond { h = e }`, so a loop that runs zero times still never evaluates it. Total expressions are hoisted
/// unguarded, which lets them climb out of several nested loops one level per pass.
module Features.Opt.Licm

open System.Collections.Generic
open Core
open Typer
open Opt

let private licm (cx: Ctx) (cond: E) (body: S list) : S list =
    let loop = newSum ()
    sumE cx loop cond
    List.iter (sumS cx loop) body
    let g = newSum ()
    sumE cx g cond
    let guard = not g.effect && not g.partial && g.writes.Count = 0
    let hoisted = ResizeArray<string * E * bool>() // name, value, guarded
    let byKey = Dictionary<string, string>()

    /// Some guarded when `e` can be computed once before the loop.
    let candidate (unconditional: bool) (first: bool) (e: E) =
        match pureInputs cx e with
        | Some(partial, inputs) ->
            let invariant = inputs |> Set.forall (fun u -> not (loop.writes.Contains u))
            let refsStable = not loop.refWrite || inputs |> Set.forall (refVar cx >> not)
            // `first`: the loop condition, evaluated once even when the loop never runs, needs no guard,
            // unless an effect in it (`(println: "x"; i < h:z)`) must happen before a failure
            let placeable = not partial || first || (unconditional && guard && not loop.effect)
            if invariant && refsStable && placeable then Some(partial && not first) else None
        | None -> None

    let replace (e: E) (guarded: bool) =
        let key = exprKey e
        if not (byKey.ContainsKey key) then
            let h = freshTmp "licm"
            cx.varTy.[h] <- e.t
            byKey.[key] <- h
            hoisted.Add(h, e, guarded)
        useVar byKey.[key] e.t

    let rec rw (unconditional: bool) (first: bool) (e: E) : E =
        match candidate unconditional first e with
        | Some guarded -> replace e guarded
        | None ->
            let go = rw unconditional first
            match e.k with
            | Bin(("&" | "|") as op, a, b) when resolve a.t = TBool -> { e with k = Bin(op, go a, rw false false b) }
            | If(c, a, b) -> { e with k = If(go c, rw false false a, rw false false b) }
            | _ -> mapE go (List.map (rwS unconditional)) e

    // an inner loop's condition runs whenever we get here; its body may run zero times
    and rwS (unconditional: bool) (s: S) : S = mapS (rw unconditional false) (List.map (rwS false)) s

    let cond' = rw true (not g.effect) cond
    let body' = List.map (rwS true) body
    let pre =
        hoisted
        |> Seq.map (fun (h, value, guarded) ->
            let a = Assign(h, value)
            if guarded then Do(typed (If(cond, typed (Seq([ a ], zero)) TInt, zero)) TInt) else a)
        |> List.ofSeq
    pre @ [ While(cond', body') ]

let rec private optStmts (cx: Ctx) (ss: S list) : S list = ss |> List.collect (optStmt cx)

and private optStmt (cx: Ctx) (s: S) : S list =
    match s with
    | While(c, body) ->
        let c = optE cx c
        let body = optStmts cx body // inner loops first: their invariants land in this body
        licm cx c body
    | s -> [ mapS (optE cx) (optStmts cx) s ]

and private optE (cx: Ctx) (e: E) : E = mapE (optE cx) (optStmts cx) e

let pass : Opt.Pass =
    { name = "licm"
      order = 30
      run = fun cx prog ->
        for i in prog.insts do
            i.body <- optStmts cx i.body
            i.result <- optE cx i.result
        { prog with main = optStmts cx prog.main } }
