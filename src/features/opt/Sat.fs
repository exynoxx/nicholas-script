/// Static-argument hoisting in recursive functions.
///
/// The recursive analogue of LICM. In a self-recursive function, a parameter that every recursive call passes on
/// unchanged is *static*; an expression reading only static parameters and captures/globals the function never
/// writes has the same value in every call of one recursion. Split the function into
///   f(ps)          = (h1 = e1; ...; f'(ps, h1, ...))        the wrapper keeps f's name, so outside callers are unchanged
///   f'(ps, h1, ..) = body with e_j -> h_j and f(...) -> f'(..., h1, ...)
/// so each e_j runs once per outside call instead of once per recursive call.
/// The wrapper evaluates e_j up front, even when the body would have taken a base case that never reaches it, so a
/// conditionally evaluated e_j must be total; an e_j on every path through the body may be partial.
module Features.Opt.Sat

open System.Collections.Generic
open Core
open Typer
open Opt

let private mentions (f: string) (e: E) =
    let mutable found = false
    e |> iterE (fun x -> match x.k with UserCall(g, _) | BlockRef g when g = f -> found <- true | _ -> ()) ignore
    found

/// Splits `i` into wrapper + worker when it has hoistable static computations; returns the worker.
let private satInst (cx: Ctx) (i: Inst) : Inst option =
    let calls = ResizeArray<E list>()
    let selfCall (e: E) = match e.k with UserCall(g, xs) when g = i.name -> calls.Add xs | _ -> ()
    List.iter (iterS selfCall ignore) i.body
    iterE selfCall ignore i.result
    let isFnTy t = match resolve t with TFn _ -> true | _ -> false
    if calls.Count = 0 || i.parms |> List.exists (snd >> isFnTy) then None
    else
        let local = newSum ()
        List.iter (sumS cx local) i.body
        sumE cx local i.result
        let ext = cx.info.[i.name]
        let statics =
            i.parms
            |> List.mapi (fun j (p, _) -> j, p)
            |> List.filter (fun (j, p) ->
                not (local.writes.Contains p)
                && calls |> Seq.forall (fun args -> args.Length > j && (match args.[j].k with Var q -> q = p | _ -> false)))
            |> List.map snd
            |> set
        // what stays fixed for a whole recursion: static parameters, and captures/globals nobody in it writes
        let fixedVar (u: string) =
            statics.Contains u || (not (i.owned.Contains u) && not (ext.writes.Contains u))
        let hs = ResizeArray<string * E>()
        let byKey = Dictionary<string, string>()
        let candidate (always: bool) (e: E) =
            match pureInputs cx e with
            | Some(partial, inputs) ->
                not (mentions i.name e)
                && inputs |> Set.forall fixedVar
                && (not ext.refWrite || inputs |> Set.forall (refVar cx >> not))
                && (not partial || (always && not ext.effect))
            | None -> false
        let replace (e: E) =
            let key = exprKey e
            if not (byKey.ContainsKey key) then
                let h = freshTmp "sat"
                cx.varTy.[h] <- e.t
                byKey.[key] <- h
                hs.Add(h, e)
            useVar byKey.[key] e.t
        let rec rw (always: bool) (e: E) : E =
            if candidate always e then replace e
            else
                match e.k with
                | Bin(("&" | "|") as op, a, b) when resolve a.t = TBool -> { e with k = Bin(op, rw always a, rw false b) }
                | If(c, a, b) -> { e with k = If(rw always c, rw false a, rw false b) }
                | _ -> mapE (rw always) (List.map (rwS always)) e
        and rwS (always: bool) (s: S) : S = mapS (rw always) (List.map (rwS false)) s
        let body = List.map (rwS true) i.body
        let result = rw true i.result
        if hs.Count = 0 then None
        else
            let wname = i.name + "_w"
            let extra = hs |> Seq.map (fun (h, e) -> typed (Var h) e.t) |> List.ofSeq
            // recursive calls go straight to the worker, handing the hoisted values down
            let rec redirect (e: E) : E =
                match e.k with
                | UserCall(f, xs) when f = i.name -> { e with k = UserCall(wname, List.map redirect xs @ extra) }
                | _ -> mapE redirect (List.map redirectS) e
            and redirectS (s: S) = mapS redirect (List.map redirectS) s
            let owned = HashSet<string>(i.owned)
            for (h, _) in hs do owned.Add h |> ignore
            let worker: Inst =
                { name = wname
                  parms = i.parms @ (hs |> Seq.map (fun (h, e) -> h, e.t) |> List.ofSeq)
                  caps = i.caps
                  ret = i.ret
                  body = List.map redirectS body
                  result = redirect result
                  owned = owned
                  calls = HashSet<string>(i.calls |> Seq.map (fun c -> if c = i.name then wname else c))
                  borrowed = HashSet() }
            cx.info.[wname] <- cx.info.[i.name]
            // the wrapper: compute the static values once, then enter the worker
            let parmVars = i.parms |> List.map (fun (p, t) -> typed (Var p) t)
            i.body <- hs |> Seq.map (fun (h, e) -> Assign(h, e)) |> List.ofSeq
            i.result <- typed (UserCall(wname, parmVars @ extra)) i.ret
            i.calls.Clear()
            i.calls.Add wname |> ignore
            for (h, _) in hs do i.owned.Add h |> ignore
            Some worker

let pass : Opt.Pass =
    { name = "sat"
      order = 10
      run = fun cx prog -> { prog with insts = prog.insts @ List.choose (satInst cx) prog.insts } }
