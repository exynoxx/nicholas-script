/// Stage 5.5: typed core -> typed core. The optimiser kernel: effect analysis of every function instance (what
/// calling it reads, writes, mutates, whether it has effects or may fail), the traversal helpers the passes share,
/// and the pass registry. Each pass lives in `features/opt/` and is added to `passes`; `run` analyses the program
/// once, then runs every registered pass in `order`.
module Opt

open System.Collections.Generic
open Core
open Typer

/// What evaluating some core does.
type Sum =
    { reads: HashSet<string>
      writes: HashSet<string>   // assigned or mutated in place
      mutates: HashSet<string>  // mutated in place (push, x@i = v, add, del, merge)
      mutable refWrite: bool    // mutates a map/set/queue/tree in place: any alias may change
      mutable effect: bool      // I/O or an unknown builtin: never moved
      mutable partial: bool }   // may panic or may not terminate

let newSum () =
    { reads = HashSet(); writes = HashSet(); mutates = HashSet(); refWrite = false; effect = false; partial = false }

let merge (into: Sum) (s: Sum) =
    into.reads.UnionWith s.reads
    into.writes.UnionWith s.writes
    into.mutates.UnionWith s.mutates
    into.refWrite <- into.refWrite || s.refWrite
    into.effect <- into.effect || s.effect
    into.partial <- into.partial || s.partial

// ---------- tables ----------

/// Core builtins that only compute a value and cannot fail.
let totalBuiltins = HashSet<string>([ "len"; "neg"; "not"; "copy"; "has"; "zero" ])
/// Core builtins that only compute a value but may panic.
let partialBuiltins = HashSet<string>()
/// Core builtins that mutate their first argument in place.
let mutatingBuiltins = HashSet<string>()

/// Stdlib functions (runtime.c `//@` names) without side effects. Anything else is an effect.
/// TODO: move this into runtime.c as a `//@ pure name (...)` marker so new stdlib functions declare it themselves.
let pureStd =
    HashSet<string>(
        [ "rev"; "chars"; "split"; "lower"; "upper"; "ws"; "isupper"; "islower"; "isdigit"; "ord"; "chr"; "join"
          "sort"; "keys"; "vals"; "arr"; "min"; "max"; "hex"; "gcd"; "pow"; "zip"; "uniq"; "find"; "bsearch"; "sortby"
          "minkey"; "maxkey"; "flatten"; "pairwise"; "tuple2"; "tuple3"; "nums"; "take"; "items"; "nb4"; "nb8" ])
/// Of those, the ones that never fail.
let totalStd = HashSet<string>([ "rev"; "chars"; "split"; "lower"; "upper"; "ws"; "isupper"; "islower"; "isdigit"; "join"; "sort"; "keys"; "vals"; "arr"; "hex"; "zip"; "uniq"; "pairwise"; "nums"; "nb4"; "nb8" ])

let rec isRef t =
    match resolve t with
    | TMap _ | TCon _ -> true
    | TArr e -> isRef e
    | TTup ts -> List.exists isRef ts
    | _ -> false

/// Types whose value can be computed early and shared: no reference types (each call would have built a fresh
/// map the loop could mutate), no blocks.
let rec hoistableTy t =
    match resolve t with
    | TInt | TFloat | TBool | TStr -> true
    | TArr e -> hoistableTy e
    | TTup ts -> List.forall hoistableTy ts
    | _ -> false

let isTemp (u: string) = u.Contains '%'

let rec root (e: E) =
    match e.k with
    | Var u -> Some u
    | Index(x, _) | Conv x -> root x
    | _ -> None

/// The loops Lower emits (`c < n` over a snapshot or a range) always terminate.
let isCounted (c: E) =
    match c.k with
    | Bin("<", { k = Var a }, { k = Var b }) -> a.StartsWith "c%" && b.StartsWith "n%"
    | _ -> false

let rec containsCall (e: E) =
    match e.k with
    | UserCall _ | StdCall _ | Seq _ -> true
    | Call(f, xs) -> f = "repeat" || f = "slice" || f = "concat" || f = "append" || List.exists containsCall xs
    | Arr xs -> List.exists containsCall xs
    | Bin(_, a, b) | Index(a, b) -> containsCall a || containsCall b
    | If(a, b, c) -> containsCall a || containsCall b || containsCall c
    | Conv x -> containsCall x
    | _ -> false

/// Rebuilds `e` with `f` applied to each direct child expression and `fs` to a Seq's statements.
let mapE (f: E -> E) (fs: S list -> S list) (e: E) : E =
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
let mapS (f: E -> E) (fs: S list -> S list) (s: S) : S =
    match s with
    | Line _ -> s
    | Do e -> Do(f e)
    | Assign(u, e) -> Assign(u, f e)
    | SetIdx(x, i, v) -> SetIdx(f x, f i, f v)
    | While(c, b) -> While(f c, fs b)

/// Calls `fe` on every expression and `fs` on every statement nested in `e`.
let rec iterE (fe: E -> unit) (fs: S -> unit) (e: E) =
    fe e
    let go = iterE fe fs
    match e.k with
    | Arr xs | Call(_, xs) | UserCall(_, xs) | StdCall(_, xs) -> List.iter go xs
    | Bin(_, a, b) | Index(a, b) -> go a; go b
    | If(a, b, c) -> go a; go b; go c
    | Conv x -> go x
    | Seq(ss, x) -> List.iter (iterS fe fs) ss; go x
    | _ -> ()

and iterS fe fs (s: S) =
    fs s
    match s with
    | Do e | Assign(_, e) -> iterE fe fs e
    | SetIdx(a, b, c) -> List.iter (iterE fe fs) [ a; b; c ]
    | While(c, b) -> iterE fe fs c; List.iter (iterS fe fs) b
    | Line _ -> ()

let typed k t = { k = k; t = t }

let exprKey (e: E) = showE e + ":" + showTy e.t

/// A read of `u`; arrays are values, so every use gets its own copy, as `own` does for `x = u`.
let useVar (u: string) (t: Ty) =
    let v = typed (Var u) t
    match resolve t with
    | TArr _ -> typed (Call("copy", [ v ])) t
    | _ -> v

/// Names for hoisted values (`licm%n`, `sat%n`).
let mutable tmpCount = 0
let freshTmp (prefix: string) = tmpCount <- tmpCount + 1; $"{prefix}%%{tmpCount}"

// ---------- effect summaries ----------

type Ctx = { info: Dictionary<string, Sum>; varTy: Dictionary<string, Ty> }

let rec sumE (cx: Ctx) (s: Sum) (e: E) =
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
        if not (pureStd.Contains sg.name) then
            s.effect <- true
            // may mutate a collection argument in place (pop, btadd, keep, ...)
            match args |> List.tryFind (fun x -> (match x.k with Desc -> false | _ -> true) && isRef x.t) with
            | Some x ->
                root x |> Option.iter (fun u -> s.writes.Add u |> ignore; s.mutates.Add u |> ignore)
                s.refWrite <- true
            | None -> ()
        elif not (totalStd.Contains sg.name) then s.partial <- true
    | Seq(ss, x) -> List.iter (sumS cx s) ss; go x
    | Iter _ | Fn _ -> s.effect <- true // gone after typing; be safe if not

and sumS (cx: Ctx) (s: Sum) (st: S) =
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
let analyse (prog: Program) =
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
let pureInputs (cx: Ctx) (e: E) : (bool * Set<string>) option =
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
let refVar (cx: Ctx) (u: string) =
    match cx.varTy.TryGetValue u with
    | true, t -> isRef t
    | _ -> true

let zero = typed (Int 0L) TInt

// ---------- pass registry ----------

/// An optimisation pass over the whole program; passes run in ascending `order`. A pass may update instances in
/// place (they are mutable records) and returns the program with its new main/insts.
type Pass = { name: string; order: int; run: Ctx -> Program -> Program }

let passes = ResizeArray<Pass>()

/// Analyses every function instance, then runs every registered pass, sorted by order.
let run (prog: Program) : Program =
    let cx = analyse prog
    passes |> Seq.sortBy (fun p -> p.order) |> Seq.fold (fun prog p -> p.run cx prog) prog
