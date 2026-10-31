/// String building.
///
/// Strings are immutable and exactly sized: `a + b + c` allocates per `+` (and per operand converted to str),
/// `xs{str}.join` a string per element plus the array, and `s = s + x` in a loop copies s each time (quadratic).
/// These write into one runtime builder instead (runtime.c `ns_sb_*`, an A<B> whose buffer becomes the string):
///   join(loop pushing v into r)   r is the builder; v goes in as itself when it is a conversion to str
///   a + b + c (3+ parts)           one builder, every part put once, in order
///   s = s + e in a loop            when nothing else in the loop (callees included) reads or writes s: the builder
///                                  starts with s before the loop, appends put e's parts, s = done after it
/// Runs after the other passes: builder calls are effects, which would block LICM, CSE and DCE of what they replace.
/// Also: slices used on the spot are compared or searched in place instead of built (`ns_subeq`, `ns_subfind`).
module Features.Opt.StrBuild

open System.Collections.Generic
open Core
open Typer
open Opt

let private sbSig name parms ret tvars : Stdlib.Sig =
    { name = name; parms = parms; ret = ret; csym = "ns_" + name; tvars = tvars; rdesc = false }
let private sbNew = sbSig "sb_new" [] (Stdlib.SArr Stdlib.SBool) []
let private sbPut = sbSig "sb_put" [ Stdlib.SArr Stdlib.SBool; Stdlib.SVar "T" ] Stdlib.SInt [ "T" ]
let private sbDone = sbSig "sb_done" [ Stdlib.SArr Stdlib.SBool; Stdlib.SStr ] Stdlib.SStr []

let rec private strParts (e: E) : E list =
    match e.k with
    | Bin("+", a, b) when resolve e.t = TStr -> strParts a @ strParts b
    | Seq([], x) -> strParts x
    | Str "" -> []
    | _ -> [ e ]

let private newBuilder (cx: Ctx) =
    let w = freshTmp "sb"
    cx.varTy.[w] <- TArr TBool
    typed (Var w) (TArr TBool), Assign(w, typed (StdCall(sbNew, [])) (TArr TBool))

/// Appends `x` as `str` would show it: a conversion to str is skipped, the builder formats the value itself.
let private sbPutS (w: E) (x: E) =
    let rec bare (x: E) = match x.k with Seq([], y) | Call("copy", [ y ]) -> bare y | Conv y when resolve x.t = TStr -> bare y | _ -> x
    let x = bare x
    Do(typed (StdCall(sbPut, [ typed Desc x.t; w; x ])) TInt)

let private sbDoneE (w: E) (sep: E) = typed (StdCall(sbDone, [ w; sep ])) TStr

// slices used on the spot: compared or searched in place instead of built (`ns_subeq`, `ns_subfind`)
let private subEq =
    sbSig "subeq" [ Stdlib.SVar "T"; Stdlib.SInt; Stdlib.SInt; Stdlib.SVar "T"; Stdlib.SInt; Stdlib.SInt ] Stdlib.SBool [ "T" ]
let private subFind = sbSig "subfind" [ Stdlib.SStr; Stdlib.SInt; Stdlib.SInt; Stdlib.SStr ] Stdlib.SInt []
let rec private sliceOf (e: E) =
    match e.k with
    | Seq([], x) -> sliceOf x
    | Call("slice", [ x; i; j ]) -> Some [ x; i; j ]
    | _ -> None

/// Rewrites every statement list nested in `ss` with `f`, innermost first.
let rec private rewriteStmts (f: S -> S list) (ss: S list) : S list =
    ss |> List.collect (fun s -> f (mapS (rewriteE f) (rewriteStmts f) s))
and private rewriteE f (e: E) = mapE (rewriteE f) (rewriteStmts f) e

let private countVar (u: string) (ss: S list) =
    let mutable n = 0
    List.iter (iterS (fun e -> match e.k with Var x when x = u -> n <- n + 1 | _ -> ()) (function Assign(x, _) when x = u -> n <- n + 1 | _ -> ())) ss
    n

/// `join(Seq(..., r = []; loop with push(r, v) ...; r), sep)`: pushes into r become puts into a builder.
let private joinLoop (cx: Ctx) (args: E list) : E option =
    let arg, sep = match args with [ _; a ] -> a, typed (Str "") TStr | [ _; a; s ] -> a, s | _ -> args.Head, args.Head
    match arg.k, sep.k with
    | Seq(ss, { k = Var r }), (Str _ | Var _) when ss |> List.exists (function Assign(q, { k = Arr [] }) -> q = r | _ -> false) ->
        let sm = newSum ()
        List.iter (sumS cx sm) ss
        let sepStable = match sep.k with Var u -> not (sm.writes.Contains u) | _ -> true
        let w, init = newBuilder cx
        let mutable pushes = 0
        let ss' =
            ss |> rewriteStmts (fun st ->
                match st with
                | Assign(q, { k = Arr [] }) when q = r -> [ init ]
                | Do { k = Call("push", [ { k = Var q }; v ]) } when q = r ->
                    pushes <- pushes + 1
                    sbPutS w v :: (match sep.k with Str "" -> [] | _ -> [ sbPutS w sep ])
                | st -> [ st ])
        // r was only created, pushed into and returned
        if sepStable && pushes > 0 && countVar r ss = pushes + 1 then Some(typed (Seq(ss', sbDoneE w sep)) TStr) else None
    | _ -> None

/// A loop whose only uses of the str variable `u` are appends `u = u + e` builds u in a builder.
let private appendLoop (cx: Ctx) (u: string) (cond: E) (body: S list) : (S list * S) option =
    let w, init = newBuilder cx
    let mutable appends = 0
    let body' =
        body |> rewriteStmts (fun st ->
            match st with
            | Assign(q, e) when q = u && resolve e.t = TStr ->
                match strParts e with
                | { k = Var x } :: rest when x = u ->
                    appends <- appends + 1
                    List.map (sbPutS w) rest
                | _ -> [ st ]
            // an assignment as a block's (dropped) value reads u
            | Do({ k = Seq(ss, { k = Var x }) } as e) when x = u -> [ Do({ e with k = Seq(ss, zero); t = TInt }) ]
            | st -> [ st ])
    let sm = newSum ()
    sumE cx sm cond
    List.iter (sumS cx sm) body'
    if appends > 0 && not (sm.reads.Contains u) && not (sm.writes.Contains u) then
        Some([ init; sbPutS w (typed (Var u) TStr); While(cond, body') ], Assign(u, sbDoneE w (typed (Str "") TStr)))
    else None

let rec private strStmts (cx: Ctx) (ss: S list) : S list = ss |> List.collect (strStmt cx)

and private strStmt (cx: Ctx) (s: S) : S list =
    match s with
    | While(c, body) ->
        // the str variables this loop appends to (outermost loop first, so nested appends join one builder)
        let appended = HashSet<string>()
        body |> List.iter (iterS ignore (function
            | Assign(u, e) when resolve e.t = TStr -> (match strParts e with { k = Var x } :: _ when x = u -> appended.Add u |> ignore | _ -> ())
            | _ -> ()))
        let mutable loop = [ While(c, body) ]
        let mutable post = []
        for u in appended do
            match List.last loop with
            | While(c, b) ->
                match appendLoop cx u c b with
                | Some(ss, p) -> loop <- List.take (loop.Length - 1) loop @ ss; post <- p :: post
                | None -> ()
            | _ -> ()
        let loop = loop |> List.map (function While(c, b) -> While(strE cx c, strStmts cx b) | st -> mapS (strE cx) (strStmts cx) st)
        loop @ post
    | _ -> [ mapS (strE cx) (strStmts cx) s ]

and private strE (cx: Ctx) (e: E) : E =
    match e.k with
    | StdCall(sg, args) when sg.name = "join" && (match sg.parms with Stdlib.SArr _ :: _ -> true | _ -> false) ->
        match joinLoop cx args with
        | Some j -> mapE (strE cx) (strStmts cx) j
        | None -> mapE (strE cx) (strStmts cx) e
    | Bin("+", _, _) when resolve e.t = TStr && (strParts e).Length >= 3 ->
        let w, init = newBuilder cx
        let puts = strParts e |> List.map (strE cx >> sbPutS w)
        typed (Seq(init :: puts, sbDoneE w (typed (Str "") TStr))) TStr
    | Bin(("=" | "!=") as op, a, b) when (match resolve a.t with TStr | TArr _ -> true | _ -> false)
                                         && ((sliceOf a).IsSome || (sliceOf b).IsSome) ->
        let whole (x: E) = sliceOf x |> Option.defaultValue [ x; typed (Int 0L) TInt; typed (Int System.Int64.MaxValue) TInt ]
        let eq = typed (StdCall(subEq, typed Desc a.t :: List.map (strE cx) (whole a @ whole b))) TBool
        if op = "=" then eq else typed (Call("not", [ eq ])) TBool
    | StdCall(sg, [ s; x ]) when sg.csym = "ns_find_str" && (sliceOf s).IsSome ->
        typed (StdCall(subFind, List.map (strE cx) ((sliceOf s).Value @ [ x ]))) TInt
    | _ -> mapE (strE cx) (strStmts cx) e

let pass : Opt.Pass =
    { name = "strbuild"
      order = 60
      run = fun cx prog ->
        let main = strStmts cx prog.main // main first: keeps the builder temporaries' numbering
        for i in prog.insts do
            i.body <- strStmts cx i.body
            i.result <- strE cx i.result
        { prog with main = main } }
