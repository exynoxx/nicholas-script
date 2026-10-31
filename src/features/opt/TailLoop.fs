/// Self tail calls -> loops.
///
/// A self call in tail position (the result, through If branches and a Seq's last value) becomes a jump back to the
/// top: f's body runs in `while go { go = F; body; r = result }`, where a tail call evaluates its arguments into
/// temporaries, assigns them to the parameters and sets go. Guaranteed, unlike LLVM's tail-recursion elimination,
/// which gives up when a parameter is released after the call (a non-borrowed string/array/map). A parameter every
/// tail call passes on unchanged (itself, or `copy` of an alias `a = p` that is never written) is not reassigned,
/// and its alias is bound once before the loop, so an array parameter isn't copied per iteration.
module Features.Opt.TailLoop

open Core
open Typer
open Opt

let private tailLoop (cx: Ctx) (i: Inst) =
    let isFnTy t = match resolve t with TFn _ -> true | _ -> false
    let rec tails (e: E) : E list list =
        match e.k with
        | UserCall(g, xs) when g = i.name -> [ xs ]
        | If(_, a, b) -> tails a @ tails b
        | Seq(_, x) -> tails x
        | _ -> []
    let calls = tails i.result
    if not calls.IsEmpty && not (i.parms |> List.exists (snd >> isFnTy)) then
        let s = newSum ()
        List.iter (sumS cx s) i.body
        sumE cx s i.result
        let assigns (u: string) =
            let mutable n = 0
            List.iter (iterS ignore (function Assign(x, _) when x = u -> n <- n + 1 | _ -> ())) i.body
            iterE ignore (function Assign(x, _) when x = u -> n <- n + 1 | _ -> ()) i.result
            n
        // a top-level `a = p` / `a = copy(p)` whose alias is never written again
        let alias =
            i.body
            |> List.choose (function
                | Assign(a, { k = (Var p | Call("copy", [ { k = Var p } ])) }) when assigns a = 1 && not (s.mutates.Contains a) -> Some(a, p)
                | _ -> None)
            |> dict
        let passesOn (p: string) (x: E) =
            match x.k with
            | Var q | Call("copy", [ { k = Var q } ]) -> q = p || (alias.ContainsKey q && alias.[q] = p)
            | _ -> false
        let statics =
            i.parms
            |> List.mapi (fun j (p, _) -> j, p)
            |> List.filter (fun (j, p) -> not (s.writes.Contains p) && calls |> List.forall (fun xs -> passesOn p xs.[j]))
            |> List.map snd
            |> set
        let fresh prefix t =
            let u = freshTmp prefix
            cx.varTy.[u] <- t
            i.owned.Add u |> ignore
            u, typed (Var u) t
        let go, goV = fresh "tail" TBool
        let r, rV = fresh "tail" i.ret
        let setGo b = Assign(go, typed (Bool b) TBool)
        let rec tailS (e: E) : S list =
            match e.k with
            | UserCall(g, xs) when g = i.name ->
                let moves =
                    List.zip i.parms xs
                    |> List.filter (fun ((p, _), _) -> not (statics.Contains p))
                    |> List.map (fun ((p, t), x) -> p, fresh "tail" t, x)
                [ for (_, (t, _), x) in moves -> Assign(t, x) ] @ [ for (p, (_, tv), _) in moves -> Assign(p, tv) ] @ [ setGo true ]
            | If(c, a, b) when not (tails e).IsEmpty ->
                let branch x = typed (Seq(tailS x, zero)) TInt
                [ Do(typed (If(c, branch a, branch b)) TInt) ]
            | Seq(ss, x) when not (tails x).IsEmpty -> ss @ tailS x
            | _ -> [ Assign(r, e) ]
        let isStaticAlias st = match st with Assign(a, _) -> alias.ContainsKey a && statics.Contains alias.[a] | _ -> false
        let pre, loop = i.body |> List.partition isStaticAlias
        i.body <- pre @ [ setGo true; While(goV, setGo false :: loop @ tailS i.result) ]
        i.result <- rV

let pass : Opt.Pass =
    { name = "tailLoop"
      order = 20
      run = fun cx prog -> List.iter (tailLoop cx) prog.insts; prog }
