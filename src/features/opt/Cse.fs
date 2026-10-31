/// Redundant expression elimination.
///
/// GVN-style reuse for pure calls. After `u = e` with e pure, a later e reads u instead, until a statement writes u
/// or one of e's inputs. Nothing is moved, so this is safe for partial e too. LLVM can't do this itself: NS calls
/// retain, release and allocate, so they are never `memory(none)` to it.
module Features.Opt.Cse

open Core
open Typer
open Opt

type private Avail = { var: string; reads: Set<string>; refs: bool }

let private killBy (s: Sum) (avail: Map<string, Avail>) =
    avail |> Map.filter (fun _ a ->
        not (s.writes.Contains a.var) && not (a.reads |> Set.exists s.writes.Contains) && not (s.refWrite && a.refs))

let rec private cseE (cx: Ctx) (avail: Map<string, Avail>) (e: E) : E =
    let hit = if containsCall e && not avail.IsEmpty then Map.tryFind (exprKey e) avail else None
    match hit with
    | Some a -> useVar a.var e.t
    | None ->
        match e.k with
        | Seq(ss, x) -> let ss, av = cseStmts cx avail ss in { e with k = Seq(ss, cseE cx av x) }
        | _ -> mapE (cseE cx avail) id e

/// Rewrites a statement list; returns it with what is still available after it. Entries made inside nested
/// blocks (branches, loop bodies) never escape them.
and private cseStmts (cx: Ctx) (avail: Map<string, Avail>) (ss: S list) : S list * Map<string, Avail> =
    let out = ResizeArray<S>()
    let mutable av = avail
    for st in ss do
        let sm = newSum ()
        sumS cx sm st
        // drop first what this statement writes anywhere inside it: then every use below sees the entry's value
        av <- killBy sm av
        match st with
        | Line _ -> out.Add st
        | Do e -> out.Add(Do(cseE cx av e))
        | SetIdx(x, i, v) -> out.Add(SetIdx(cseE cx av x, cseE cx av i, cseE cx av v))
        | While(c, body) ->
            let body, _ = cseStmts cx av body
            out.Add(While(cseE cx av c, body))
        | Assign(u, e) ->
            out.Add(Assign(u, cseE cx av e))
            match pureInputs cx e with
            | Some(_, inputs) when not (inputs.Contains u) ->
                av <- av.Add(exprKey e, { var = u; reads = inputs; refs = Set.exists (refVar cx) inputs })
            | _ -> ()
    List.ofSeq out, av

let pass : Opt.Pass =
    { name = "cse"
      order = 40
      run = fun cx prog ->
        for i in prog.insts do
            let body, av = cseStmts cx Map.empty i.body
            i.body <- body
            i.result <- cseE cx av i.result
        { prog with main = fst (cseStmts cx Map.empty prog.main) } }
