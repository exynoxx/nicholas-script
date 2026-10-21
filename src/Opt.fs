/// Stage 5.5: typed core -> typed core. Effect analysis of every function instance, then loop-invariant code
/// motion (LICM): a pure expression inside a `While` whose inputs the loop never writes is computed once before it.
///
///   for each While (innermost first):
///     written  = every variable the loop assigns, pushes into or SetIdx's, plus what its callees write
///     hoist e  when  e is pure, reads nothing in `written`, has a hoistable type and contains a call
///
/// A *partial* expression (may panic or not terminate: index, `/`, recursion, user loops) is only hoisted under a
/// guard `if cond { h = e }`, so a loop that runs zero times still never evaluates it. Total expressions are hoisted
/// unguarded, which lets them climb out of several nested loops one level per pass.
module Opt

open System.Collections.Generic
open Core
open Typer

/// What evaluating some core does.
type private Sum =
    { reads: HashSet<string>
      writes: HashSet<string>   // assigned or mutated in place
      mutates: HashSet<string>  // mutated in place (push, x@i = v, add, del, merge)
      mutable refWrite: bool    // mutates a map/set/queue/tree/graph in place: any alias may change
      mutable effect: bool      // I/O or an unknown builtin: never moved
      mutable partial: bool }   // may panic or may not terminate

let private newSum () =
    { reads = HashSet(); writes = HashSet(); mutates = HashSet(); refWrite = false; effect = false; partial = false }

let private merge (into: Sum) (s: Sum) =
    into.reads.UnionWith s.reads
    into.writes.UnionWith s.writes
    into.mutates.UnionWith s.mutates
    into.refWrite <- into.refWrite || s.refWrite
    into.effect <- into.effect || s.effect
    into.partial <- into.partial || s.partial

// ---------- tables ----------

/// Core builtins that only compute a value and cannot fail.
let private totalBuiltins = set [ "len"; "neg"; "not"; "tget"; "copy"; "append"; "concat"; "slice"; "tuple"; "has"; "range"; "zero" ]
/// Core builtins that only compute a value but may panic.
let private partialBuiltins = set [ "repeat"; "peek" ]
/// Core builtins that mutate their first argument in place.
let private mutatingBuiltins = set [ "push"; "add"; "del"; "merge" ]

/// Stdlib functions (runtime.c `//@` names) without side effects. Anything else is an effect.
/// TODO: move this into runtime.c as a `//@ pure name (...)` marker so new stdlib functions declare it themselves.
let private pureStd =
    set [ "rev"; "chars"; "split"; "lower"; "upper"; "ws"; "isupper"; "islower"; "isdigit"; "ord"; "chr"; "join"
          "sort"; "keys"; "vals"; "arr"; "min"; "max"; "hex"; "gcd"; "pow"; "zip"; "uniq"; "find"; "bsearch"; "sortby"
          "minkey"; "maxkey"; "flatten"; "pairwise"; "tuple2"; "tuple3"; "nums"; "take"; "items" ]
/// Of those, the ones that never fail.
let private totalStd = set [ "rev"; "chars"; "split"; "lower"; "upper"; "ws"; "isupper"; "islower"; "isdigit"; "join"; "sort"; "keys"; "vals"; "arr"; "hex"; "zip"; "uniq"; "pairwise"; "nums" ]

let rec private isRef t =
    match resolve t with
    | TMap _ | TSet _ | TQ _ | TPQ _ | TTree _ | TGraph _ -> true
    | TArr e -> isRef e
    | TTup ts -> List.exists isRef ts
    | _ -> false

/// Types whose value can be computed early and shared: no reference types (each call would have built a fresh
/// map the loop could mutate), no blocks.
let rec private hoistableTy t =
    match resolve t with
    | TInt | TFloat | TBool | TStr -> true
    | TArr e -> hoistableTy e
    | TTup ts -> List.forall hoistableTy ts
    | _ -> false

let private isTemp (u: string) = u.Contains '%'

let rec private root (e: E) =
    match e.k with
    | Var u -> Some u
    | Index(x, _) | Conv x -> root x
    | _ -> None

/// The loops Lower emits (`c < n` over a snapshot or a range) always terminate.
let private isCounted (c: E) =
    match c.k with
    | Bin("<", { k = Var a }, { k = Var b }) -> a.StartsWith "c%" && b.StartsWith "n%"
    | _ -> false

let rec private containsCall (e: E) =
    match e.k with
    | UserCall _ | StdCall _ | Seq _ -> true
    | Call(f, xs) -> f = "repeat" || f = "slice" || f = "concat" || f = "append" || List.exists containsCall xs
    | Arr xs -> List.exists containsCall xs
    | Bin(_, a, b) | Index(a, b) -> containsCall a || containsCall b
    | If(a, b, c) -> containsCall a || containsCall b || containsCall c
    | Conv x -> containsCall x
    | _ -> false

/// Rebuilds `e` with `f` applied to each direct child expression and `fs` to a Seq's statements.
let private mapE (f: E -> E) (fs: S list -> S list) (e: E) : E =
    let k =
        match e.k with
        | Arr xs -> Arr(List.map f xs)
        | Bin(op, a, b) -> Bin(op, f a, f b)
        | If(a, b, c) -> If(f a, f b, f c)
        | Call(g, xs) -> Call(g, List.map f xs)
        | UserCall(g, xs) -> UserCall(g, List.map f xs)
        | StdCall(sg, xs) -> StdCall(sg, List.map f xs)
        | Index(a, b) -> Index(f a, f b)
        | Conv x -> Conv(f x)
        | Seq(ss, x) -> Seq(fs ss, f x)
        | k -> k
    { e with k = k }

/// The same for one statement's expressions; a loop body goes through `fs`.
let private mapS (f: E -> E) (fs: S list -> S list) (s: S) : S =
    match s with
    | Line _ -> s
    | Do e -> Do(f e)
    | Assign(u, e) -> Assign(u, f e)
    | SetIdx(x, i, v) -> SetIdx(f x, f i, f v)
    | While(c, b) -> While(f c, fs b)

/// Calls `fe` on every expression and `fs` on every statement nested in `e`.
let rec private iterE (fe: E -> unit) (fs: S -> unit) (e: E) =
    fe e
    let go = iterE fe fs
    match e.k with
    | Arr xs | Call(_, xs) | UserCall(_, xs) | StdCall(_, xs) -> List.iter go xs
    | Bin(_, a, b) | Index(a, b) -> go a; go b
    | If(a, b, c) -> go a; go b; go c
    | Conv x -> go x
    | Seq(ss, x) -> List.iter (iterS fe fs) ss; go x
    | _ -> ()

and private iterS fe fs (s: S) =
    fs s
    match s with
    | Do e | Assign(_, e) -> iterE fe fs e
    | SetIdx(a, b, c) -> List.iter (iterE fe fs) [ a; b; c ]
    | While(c, b) -> iterE fe fs c; List.iter (iterS fe fs) b
    | Line _ -> ()

let private typed k t = { k = k; t = t }

let private exprKey (e: E) = showE e + ":" + showTy e.t

/// A read of `u`; arrays are values, so every use gets its own copy, as `own` does for `x = u`.
let private useVar (u: string) (t: Ty) =
    let v = typed (Var u) t
    match resolve t with
    | TArr _ -> typed (Call("copy", [ v ])) t
    | _ -> v

/// Names for hoisted values (`licm%n`, `sat%n`).
let mutable private tmpCount = 0
let private freshTmp (prefix: string) = tmpCount <- tmpCount + 1; $"{prefix}%%{tmpCount}"

// ---------- effect summaries ----------

type private Ctx = { info: Dictionary<string, Sum>; varTy: Dictionary<string, Ty> }

let rec private sumE (cx: Ctx) (s: Sum) (e: E) =
    let go = sumE cx s
    match e.k with
    | Int _ | Float _ | Str _ | Bool _ | Desc -> ()
    | Var u -> s.reads.Add u |> ignore
    | Arr xs -> List.iter go xs
    | Bin(op, a, b) ->
        go a; go b
        let constDivisor = match b.k with Int v -> v <> 0L | Float _ -> true | _ -> false
        if (op = "/" || op = "%") && not constDivisor && resolve a.t <> TFloat then s.partial <- true
        // int x ^^ -k is 1/x^k: panics when x = 0
        if op = "^^" && resolve a.t = TInt && (match b.k with Int v -> v < 0L | _ -> true) && (match a.k with Int v -> v = 0L | _ -> true) then s.partial <- true
    | If(c, a, b) -> go c; go a; go b
    | Conv x -> go x
    | Index(x, i) ->
        go x; go i
        // Lower.iter's own loads `t@c` with `c < #t`, and `s@k` with k a key of map s, are in bounds; other
        // temps (a block's renamed `_ @`, broadcast's `y`) are not
        match x.k, i.k with
        | Var t, Var c when (t.StartsWith "t%" && c.StartsWith "c%") || (t.StartsWith "s%" && c.StartsWith "k%") -> ()
        | _ -> s.partial <- true
    | Call(f, args) ->
        List.iter go args
        if mutatingBuiltins.Contains f then
            match args with
            | x :: _ ->
                root x |> Option.iter (fun u -> s.writes.Add u |> ignore; s.mutates.Add u |> ignore)
                if isRef x.t then s.refWrite <- true
            | [] -> ()
        elif f = "panic" then s.partial <- true
        elif partialBuiltins.Contains f then s.partial <- true
        elif not (totalBuiltins.Contains f) then s.effect <- true
    | UserCall(f, args) ->
        List.iter go args
        match cx.info.TryGetValue f with
        | true, fs -> merge s fs
        | _ -> s.effect <- true
    | BlockRef f ->
        match cx.info.TryGetValue f with
        | true, fs -> merge s fs
        | _ -> s.effect <- true
    | StdCall(sg, args) ->
        List.iter go args
        if not (pureStd.Contains sg.name) then s.effect <- true
        elif not (totalStd.Contains sg.name) then s.partial <- true
    | Seq(ss, x) -> List.iter (sumS cx s) ss; go x
    | Iter _ | MatchOn _ | Fn _ -> s.effect <- true // gone after typing; be safe if not

and private sumS (cx: Ctx) (s: Sum) (st: S) =
    match st with
    | Line _ -> ()
    | Do e -> sumE cx s e
    | Assign(u, e) -> s.writes.Add u |> ignore; sumE cx s e
    | SetIdx(x, i, v) ->
        sumE cx s x; sumE cx s i; sumE cx s v
        root x |> Option.iter (fun u -> s.writes.Add u |> ignore; s.mutates.Add u |> ignore)
        if isRef x.t then s.refWrite <- true
        s.partial <- true
    | While(c, body) ->
        sumE cx s c
        List.iter (sumS cx s) body
        if not (isCounted c) then s.partial <- true

/// What calling each instance does, seen from outside: reads/writes of variables it doesn't own (captures and
/// globals), its effects, and whether it may fail. Optimistic start + iteration to a fixpoint handles recursion.
let private analyse (prog: Program) =
    let info = Dictionary<string, Sum>()
    for i in prog.insts do info.[i.name] <- newSum ()
    let cx = { info = info; varTy = prog.varTy }
    let size (s: Sum) = s.reads.Count + s.writes.Count + s.mutates.Count + (if s.refWrite then 1 else 0) + (if s.effect then 1 else 0) + (if s.partial then 1 else 0)
    let mutable changed = true
    while changed do
        changed <- false
        for i in prog.insts do
            let s = newSum ()
            List.iter (sumS cx s) i.body
            sumE cx s i.result
            let parms = i.parms |> List.map fst |> set
            let ext = newSum ()
            for u in s.reads do if not (i.owned.Contains u) then ext.reads.Add u |> ignore
            for u in s.writes do if not (i.owned.Contains u) then ext.writes.Add u |> ignore
            // pushing into a parameter's array mutates the caller's array
            for u in s.mutates do
                if not (i.owned.Contains u) || parms.Contains u then (ext.writes.Add u |> ignore; ext.mutates.Add u |> ignore)
            ext.refWrite <- s.refWrite
            ext.effect <- s.effect || ext.mutates.Count > 0
            ext.partial <- s.partial
            if size ext <> size info.[i.name] then
                info.[i.name] <- ext
                changed <- true
    // a cycle in the call graph may not terminate
    let calls = prog.insts |> List.map (fun i -> i.name, i.calls) |> dict
    for i in prog.insts do
        let seen = HashSet<string>()
        let rec reach f = for g in calls.[f] do if seen.Add g && calls.ContainsKey g then reach g
        reach i.name
        if seen.Contains i.name then info.[i.name].partial <- true
    cx

/// `e`'s inputs, when it computes a value worth hoisting or reusing: it contains a call, has a hoistable type,
/// has no effect, mutates nothing and writes only its own temporaries (assigned before e reads them, so a
/// counter `(r = r + 1; 0)` that updates an outer temporary is not one, and read inside e, so tailLoop's
/// `(tail%r = "even"; 0)` that hands a value out is not one either). Also says whether it may fail.
let private pureInputs (cx: Ctx) (e: E) : (bool * Set<string>) option =
    if not (containsCall e) || not (hoistableTy e.t) then None
    else
        let s = newSum ()
        sumE cx s e
        if s.effect || s.refWrite || s.mutates.Count > 0 || not (Seq.forall isTemp s.writes)
           || not (Seq.forall s.reads.Contains s.writes) then None
        else
            // in evaluation order: a read before any write is an input; writing an input reaches outside e
            let defined, inputs = HashSet<string>(), HashSet<string>()
            let mutable outer = false
            let rec order (x: E) =
                match x.k with
                | Var u -> if not (defined.Contains u) then inputs.Add u |> ignore
                | _ -> mapE (fun c -> order c; c) (fun ss -> List.iter orderS ss; ss) x |> ignore
            and orderS (st: S) =
                match st with
                | Assign(u, rhs) -> order rhs; (if inputs.Contains u then outer <- true); defined.Add u |> ignore
                | While(c, body) -> order c; List.iter orderS body
                | st -> mapS (fun c -> order c; c) id st |> ignore
            order e
            // captures and globals its callees read
            for u in s.reads do if not (defined.Contains u) then inputs.Add u |> ignore
            if outer then None else Some(s.partial, Set.ofSeq inputs)

/// Unknown variables count as references.
let private refVar (cx: Ctx) (u: string) =
    match cx.varTy.TryGetValue u with
    | true, t -> isRef t
    | _ -> true

// ---------- in-place reuse (FBIP) ----------
//
// `x = x{f}` lowers to a loop that pushes into a fresh array r, then drops x's old array. Arrays are values with a
// single owner (assignment copies, `x@i = v` already writes in place), so when the result keeps x's element type
// and the loop never reads x, s or r, each result goes into the slot it was loaded from: `t@c = v` instead of
// `push(r, v)`, and x keeps its own array. Ranges, strings, maps and filters lower differently and don't match.

let rec private reuse (cx: Ctx) (x: string) (e: E) : E =
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

// ---------- loop-invariant code motion ----------

let private zero = typed (Int 0L) TInt

let rec private optStmts (cx: Ctx) (ss: S list) : S list = ss |> List.collect (optStmt cx)

and private optStmt (cx: Ctx) (s: S) : S list =
    match s with
    | Line _ -> [ s ]
    | Do e -> [ Do(optE cx e) ]
    | Assign(u, e) -> [ Assign(u, reuse cx u (optE cx e)) ]
    | SetIdx(x, i, v) -> [ SetIdx(optE cx x, optE cx i, optE cx v) ]
    | While(c, body) ->
        let c = optE cx c
        let body = optStmts cx body // inner loops first: their invariants land in this body
        licm cx c body

and private optE (cx: Ctx) (e: E) : E = mapE (optE cx) (optStmts cx) e

and private licm (cx: Ctx) (cond: E) (body: S list) : S list =
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

// ---------- static-argument hoisting in recursive functions ----------
//
// The recursive analogue of LICM. In a self-recursive function, a parameter that every recursive call passes on
// unchanged is *static*; an expression reading only static parameters and captures/globals the function never
// writes has the same value in every call of one recursion. Split the function into
//   f(ps)          = (h1 = e1; ...; f'(ps, h1, ...))        the wrapper keeps f's name, so outside callers are unchanged
//   f'(ps, h1, ..) = body with e_j -> h_j and f(...) -> f'(..., h1, ...)
// so each e_j runs once per outside call instead of once per recursive call.
// The wrapper evaluates e_j up front, even when the body would have taken a base case that never reaches it, so a
// conditionally evaluated e_j must be total; an e_j on every path through the body may be partial.

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

// ---------- self tail calls -> loops ----------
//
// A self call in tail position (the result, through If branches and a Seq's last value) becomes a jump back to the
// top: f's body runs in `while go { go = F; body; r = result }`, where a tail call evaluates its arguments into
// temporaries, assigns them to the parameters and sets go. Guaranteed, unlike LLVM's tail-recursion elimination,
// which gives up when a parameter is released after the call (a non-borrowed string/array/map). A parameter every
// tail call passes on unchanged (itself, or `copy` of an alias `a = p` that is never written) is not reassigned,
// and its alias is bound once before the loop, so an array parameter isn't copied per iteration.

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

// ---------- redundant expression elimination ----------
//
// GVN-style reuse for pure calls. After `u = e` with e pure, a later e reads u instead, until a statement writes u
// or one of e's inputs. Nothing is moved, so this is safe for partial e too. LLVM can't do this itself: NS calls
// retain, release and allocate, so they are never `memory(none)` to it.

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

// ---------- dead pure code ----------
//
// `u = e` with u never read, or `e` whose value is dropped, goes when e is pure and total and every variable it
// writes occurs nowhere else (its own loop temporaries). LLVM keeps these: the calls allocate and refcount.

/// Visitors that count every occurrence (read or assignment) of each variable into `d` and collect reads.
let private occurrences (d: Dictionary<string, int>) (reads: HashSet<string>) =
    let bump u = d.[u] <- (match d.TryGetValue u with | true, n -> n + 1 | _ -> 1)
    (fun (e: E) -> match e.k with Var u -> bump u; reads.Add u |> ignore | _ -> ()),
    (fun (s: S) -> match s with Assign(u, _) -> bump u | _ -> ())

let private dce (cx: Ctx) (prog: Program) (insts: Inst list) =
    let all = Dictionary<string, int>()
    let reads = HashSet<string>()
    let fe, fs = occurrences all reads
    for i in insts do
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
    for i in insts do
        i.body <- sweep i.body
    sweep prog.main

// ---------- borrowed parameters ----------
//
// Lean 4's borrow inference, simplified. Every NS call passes owned (+1) arguments that the callee releases on
// return: a retain/release pair per pointer argument per call, which LLVM can't remove (a release may free).
// A parameter the function never reassigns or mutates, in a function that writes no captures/globals and no
// reference value, is marked borrowed: callers lend it (an uncaptured local without a retain), the callee skips
// its release, and the C callback wrapper skips its retain. It also takes the release off the path after a
// self tail call, so LLVM's tail-recursion elimination can turn it into a loop.

let private inferBorrows (cx: Ctx) (insts: Inst list) =
    for i in insts do
        let s = newSum ()
        List.iter (sumS cx s) i.body
        sumE cx s i.result
        sumE cx s i.result
        let ext = cx.info.[i.name]
        i.borrowed.Clear()
        if ext.writes.Count = 0 && not ext.refWrite then
            for (p, _) in i.parms do
                if not (s.writes.Contains p) then i.borrowed.Add p |> ignore

// ---------- string building ----------
//
// Strings are immutable and exactly sized: `a + b + c` allocates per `+` (and per operand converted to str),
// `xs{str}.join` a string per element plus the array, and `s = s + x` in a loop copies s each time (quadratic).
// These write into one runtime builder instead (runtime.c `ns_sb_*`, an A<B> whose buffer becomes the string):
//   join(loop pushing v into r)   r is the builder; v goes in as itself when it is a conversion to str
//   a + b + c (3+ parts)           one builder, every part put once, in order
//   s = s + e in a loop            when nothing else in the loop (callees included) reads or writes s: the builder
//                                  starts with s before the loop, appends put e's parts, s = done after it
// Runs after the other passes: builder calls are effects, which would block LICM, CSE and DCE of what they replace.

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

/// Runs the optimisations over every function instance and main:
/// static-argument hoisting, LICM, redundant expression elimination, dead pure code, string building, borrow inference.
let run (prog: Program) : Program =
    let cx = analyse prog
    let workers = prog.insts |> List.choose (satInst cx)
    let insts = prog.insts @ workers
    List.iter (tailLoop cx) insts
    for i in insts do
        i.body <- optStmts cx i.body
        i.result <- optE cx i.result
    let mutable main = optStmts cx prog.main
    for i in insts do
        let body, av = cseStmts cx Map.empty i.body
        i.body <- body
        i.result <- cseE cx av i.result
    main <- fst (cseStmts cx Map.empty main)
    let prog = { prog with main = main; insts = insts }
    let main = dce cx prog insts |> strStmts cx
    for i in insts do
        i.body <- strStmts cx i.body
        i.result <- strE cx i.result
    inferBorrows cx insts
    { prog with main = main }
