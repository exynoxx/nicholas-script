/// Dead pure code.
///
/// `u = e` with u never read, or `e` whose value is dropped, goes when e is pure and total and every variable it
/// writes occurs nowhere else (its own loop temporaries). LLVM keeps these: the calls allocate and refcount.
module Features.Opt.Dce

open System.Collections.Generic
open Core
open Typer
open Opt

/// Visitors that count every occurrence (read or assignment) of each variable into `d` and collect reads.
let private occurrences (d: Dictionary<string, int>) (reads: HashSet<string>) =
    let bump u = d.[u] <- (match d.TryGetValue u with | true, n -> n + 1 | _ -> 1)
    (fun (e: E) -> match e.k with Var u -> bump u; reads.Add u |> ignore | _ -> ()),
    (fun (s: S) -> match s with Assign(u, _) -> bump u | _ -> ())

let private dce (cx: Ctx) (prog: Program) =
    let all = Dictionary<string, int>()
    let reads = HashSet<string>()
    let fe, fs = occurrences all reads
    for i in prog.insts do
        List.iter (iterS fe fs) i.body; iterE fe fs i.result
        for cap in i.caps do reads.Add cap |> ignore
    List.iter (iterS fe fs) prog.main
    let removable (e: E) =
        let s = newSum ()
        sumE cx s e
        let local = Dictionary<string, int>()
        let le, ls = occurrences local (HashSet())
        iterE le ls e
        not s.effect && not s.partial && not s.refWrite && s.mutates.Count = 0
        && s.writes |> Seq.forall (fun u -> isTemp u && local.[u] = all.[u])
    let rec sweep (ss: S list) : S list =
        ss |> List.choose (fun s ->
            match s with
            | Assign(u, e) when not (reads.Contains u) && removable e -> None
            | Do e when removable e -> None
            | While(c, b) -> Some(While(c, sweep b))
            | s -> Some(sweepIn s))
    and sweepIn (s: S) =
        let rec go (e: E) : E = mapE go sweep e
        mapS go id s
    for i in prog.insts do
        i.body <- sweep i.body
    { prog with main = sweep prog.main }

let pass : Opt.Pass = { name = "dce"; order = 50; run = dce }
