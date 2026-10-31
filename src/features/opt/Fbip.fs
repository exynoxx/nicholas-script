/// In-place reuse (FBIP).
///
/// `x = x{f}` lowers to a loop that pushes into a fresh array r, then drops x's old array. Arrays are values with a
/// single owner (assignment copies, `x@i = v` already writes in place), so when the result keeps x's element type
/// and the loop never reads x, s or r, each result goes into the slot it was loaded from: `t@c = v` instead of
/// `push(r, v)`, and x keeps its own array. Ranges, strings, maps and filters lower differently and don't match.
module Features.Opt.Fbip

open Core
open Typer
open Opt

let rec reuse (cx: Ctx) (x: string) (e: E) : E =
    let blind (ss: S list) (us: string list) =
        let sm = newSum ()
        List.iter (sumS cx sm) ss
        us |> List.forall (fun u -> not (sm.reads.Contains u) && not (sm.writes.Contains u))
    match e.k with
    // broadcast `x op b` binds b first
    | Seq(pre, ({ k = Seq _ } as inner)) when blind pre [ x ] -> { e with k = Seq(pre, reuse cx x inner) }
    | Seq((Assign(s, { k = Var x0 }) :: Assign(t, { k = Var s0 }) :: _) as ss, ({ k = Var r } as res))
        when x0 = x && s0 = s && (match resolve res.t with TArr _ -> true | _ -> false)
             && (match cx.varTy.TryGetValue x with | true, xt -> showTy xt = showTy res.t | _ -> false) ->
        let pushTo st = match st with Do { k = Call("push", [ { k = Var q }; v ]) } when q = r -> Some v | _ -> None
        let isLoop st =
            match st with
            | While(c, body) -> isCounted c && (body |> List.choose pushTo).Length = 1
            | _ -> false
        let isInit st = match st with Assign(q, { k = Arr [] }) -> q = r | _ -> false
        match List.filter isLoop ss, List.filter isInit ss with
        | [ While({ k = Bin(_, ({ k = Var _ } as c), _) } as cond, body) ], [ _ ] ->
            let body' = body |> List.map (fun st -> match pushTo st with Some v -> Do v | None -> st)
            let others = ss |> List.filter (fun st -> not (isLoop st) && not (isInit st))
            if blind body' [ x; s; r ] && blind (List.skip 2 others) [ r ] then
                let set st = match pushTo st with Some v -> SetIdx(typed (Var t) res.t, c, v) | None -> st
                let ss' = ss |> List.filter (not << isInit) |> List.map (fun st -> if isLoop st then While(cond, List.map set body) else st)
                { e with k = Seq(ss', typed (Var t) res.t) }
            else e
        | _ -> e
    | _ -> e

let rec private stmt (cx: Ctx) (s: S) : S =
    match s with
    | Assign(u, e) -> Assign(u, reuse cx u (expr cx e))
    | s -> mapS (expr cx) (List.map (stmt cx)) s

and private expr (cx: Ctx) (e: E) : E = mapE (expr cx) (List.map (stmt cx)) e

let pass : Opt.Pass =
    { name = "fbip"
      order = 35
      run = fun cx prog ->
        for i in prog.insts do
            i.body <- List.map (stmt cx) i.body
            i.result <- expr cx i.result
        { prog with main = List.map (stmt cx) prog.main } }
