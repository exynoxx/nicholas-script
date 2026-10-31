/// Stage 4: core -> typed core. Infers types, inserts conversions (left-operand rule),
/// resolves scopes/captures, monomorphises function blocks, and lowers type-directed forms via Lower.
module Typer

open System.Collections.Generic
open Ast
open Core

type Frame = { parent: Frame option; vars: Dictionary<string, string>; inst: Inst option }

and Inst =
    { name: string
      mutable parms: (string * Ty) list
      mutable caps: string list
      mutable ret: Ty
      mutable body: S list
      mutable result: E
      owned: HashSet<string>
      calls: HashSet<string>
      /// parameters the callee only reads: callers lend them without a retain (set by Opt)
      borrowed: HashSet<string> }

type Program =
    { main: S list
      globals: (string * Ty) list
      insts: Inst list
      varTy: Dictionary<string, Ty> }

let varTy = Dictionary<string, Ty>()
let private globals = ResizeArray<string>()
let fnDefs = Dictionary<int, S list * Frame>()
let private spreads = Dictionary<int, int>() // block id -> its tuple-taking copy (spreadBlock)
let private insts = Dictionary<string, Inst>()
let private instList = ResizeArray<Inst>()
let global0 = { parent = None; vars = Dictionary(); inst = None }
let mutable cur = global0
let mutable private line = 0
/// Editor hovers (`ns types`): (line, col, type) of each variable use, and (line, 0, type) of each assignment
/// (the target's column isn't kept; col 0 = "named on this line"). Types are resolved when printed.
let hovers = ResizeArray<int * int * string * Ty>()
/// Column of the node being typed (0: unknown, e.g. in generated code)
let mutable private col = 0
/// Type of the then-branch while typing an else-branch; resolves `rec + rec` in recursive functions.
let mutable expect: Ty option = None
/// The `x op e` of the statement `x = x op e` (also `x op= e` and a fold step): a set operator there updates x in place.
let mutable inPlace: E option = None
let mutable private counter = 0
/// Element types of empty literals ([] %[] [->]), and the blocks (instance key prefix) whose call fixed an outer one.
let private emptyEls = ResizeArray<Ty>()
let private fixers = HashSet<string>()
let err msg = failAt line col msg


let newHole () = THole(ref None)

// ---------- feature hooks (filled by Feature.register) ----------

/// Tried in order on every untyped node before the kernel's own rules; Some claims the node.
let intercept = ResizeArray<E -> E option>()
/// Primitive typing by name, on typed arguments, tried in order before the kernel's.
let prims = Dictionary<string, ResizeArray<E list -> E option>>()
/// `a op b` (typed operands; inPlace: the statement is `a = a op b`), after comparisons and before the kernel's rules.
let bins = ResizeArray<string -> bool -> E -> E -> E option>()
/// `x@i` (typed operands), by the type of x.
let indexers = ResizeArray<E -> E -> E option>()
/// `x@i = v` (typed operands), by the type of x.
let setIndexers = ResizeArray<E -> E -> E -> S option>()
/// Is a primitive call's result a fresh array nothing else references? By name; the kernel's default is yes.
let freshness = Dictionary<string, E -> bool>()
/// `u = e` (typed, u the variable's unique name) rewritten as another statement (`x = append(x, v)` -> push).
let assignRewrites = ResizeArray<string -> E -> S option>()
/// A function body rewritten before it is typed: block id, argument count, body (`memo` caches it).
let bodyRewrites = ResizeArray<int -> int -> S list -> S list>()
/// Blocks that must not be given a tuple-spreading copy for a stdlib callback.
let noSpread = ResizeArray<int -> bool>()
/// Instances whose body may fix an outer empty literal's type without counting as a fixer (lazy sink versions).
let fixerExempt = ResizeArray<int -> bool>()
/// Statements put before main, made once the whole program is typed (memo caches).
let prologue = ResizeArray<unit -> S list>()
/// Feature state to clear before a program is typed.
let resets = ResizeArray<unit -> unit>()

/// A compile error at the node being typed.
let error msg = CompileError(line, col, msg)

/// Does type t contain the unbound hole h?
let rec holeIn (h: Ty) (t: Ty) =
    match resolve h, resolve t with
    | THole a, THole b -> obj.ReferenceEquals(a, b)
    | _, TArr x -> holeIn h x
    | _, TMap(k, v) -> holeIn h k || holeIn h v
    | _, TCon(_, ts) -> List.exists (holeIn h) ts
    | _, TTup ts -> List.exists (holeIn h) ts
    | _ -> false
let typed k t = { k = k; t = t }
/// A type as errors show it: a block is "a block", not its internal fn<id>.
let showT t = match resolve t with TFn _ -> "a block" | t -> showTy t
/// The type of e, prefixed by the user variable it reads (`'xs' ([int])`), for errors.
let rec showVal (e: E) =
    match e.k with
    | Var n when not (n.Contains '%' || n.StartsWith '_') -> $"'{n.Split([| '\''; '.' |]).[0]}' ({showT e.t})"
    | Conv x -> showVal x
    | _ -> showT e.t
let isScalar t = match resolve t with TInt | TFloat | TStr | TBool -> true | _ -> false

/// Unifies two types, binding holes; when they don't unify, no hole stays bound.
let unify a b =
    let bound = ResizeArray<Ty option ref>()
    let rec go a b =
        match resolve a, resolve b with
        | THole r1, THole r2 when obj.ReferenceEquals(r1, r2) -> true
        | THole r, t | t, THole r when not (holeIn (THole r) t) -> (r.Value <- Some t; bound.Add r; true) // occurs check: no infinite types
        | TArr x, TArr y -> go x y
        | TCon(a, xs), TCon(b, ys) -> a = b && xs.Length = ys.Length && List.forall2 go xs ys
        | TMap(k1, v1), TMap(k2, v2) -> go k1 k2 && go v1 v2
        | TTup xs, TTup ys -> xs.Length = ys.Length && List.forall2 go xs ys
        | x, y -> x = y
    if go a b then true
    else
        for r in bound do r.Value <- None
        false

// ---------- scopes ----------

let declare (fr: Frame) (name: string) (ty: Ty) =
    counter <- counter + 1
    let u = $"{name}.{counter}"
    fr.vars.[name] <- u
    varTy.[u] <- ty
    match fr.inst with
    | Some i -> i.owned.Add u |> ignore
    | None -> globals.Add u
    u

/// Finds a variable lexically; a hit in an enclosing function frame becomes a capture of every frame in between.
let lookup (name: string) =
    let rec find (f: Frame) path =
        match f.vars.TryGetValue name with
        | true, u -> Some(u, f, path)
        | _ -> f.parent |> Option.bind (fun p -> find p (f :: path))
    match find cur [] with
    | Some(u, owner, path) ->
        if owner.inst.IsSome then
            for f in path do
                let i = f.inst.Value
                if not (List.contains u i.caps) then i.caps <- i.caps @ [ u ]
        Some u
    | None -> None

// ---------- conversions ----------

let castName t =
    match resolve t with
    | TInt -> "int"
    | TFloat -> "float"
    | TStr -> "str"
    | TBool -> "bool"
    | t -> err ($"no conversion to {showT t}")

/// Generated temporaries (`%` in the name) whose every assignment so far stored a fresh array; a group
/// (a lowered loop) yielding one hands it over.
let private freshTemps = Dictionary<string, bool>()

/// Does `e` build a new array that nothing else references? Variables, elements of containers (and
/// branches or groups yielding one) are shared; a slice or stdlib result of nested arrays shares the inner
/// arrays of its arguments, unless those are fresh themselves.
let rec fresh (e: E) =
    let nested = match resolve e.t with TArr el -> (match resolve el with TArr _ -> true | _ -> false) | _ -> false
    let freshArg (a: E) = match a.k, resolve a.t with Desc, _ | BlockRef _, _ -> true | _, TArr _ -> fresh a | _, t -> isScalar t
    match e.k with
    | Seq(_, { k = Var u }) -> (match freshTemps.TryGetValue u with | true, f -> f | _ -> false) // a group's own result
    | Var _ | Index _ -> false
    | Call(f, _) when freshness.ContainsKey f -> freshness.[f] e
    | StdCall(sg, args) -> (match sg.ret with Stdlib.SVar _ -> false | _ -> true) && (not nested || List.forall freshArg args)
    | If(_, a, b) -> fresh a && fresh b
    | Seq(_, x) -> fresh x
    | _ -> true

/// Arrays are values: copy when an array that is not fresh is stored somewhere else (only the branches
/// that need it); `local` variables die here, so they are handed over.
let rec ownIn (local: string -> bool) (e: E) =
    match resolve e.t, e.k with
    | TArr _, _ when fresh e -> e
    | TArr _, Var u when local u -> e
    | TArr _, If(c, a, b) -> typed (If(c, ownIn local a, ownIn local b)) e.t
    | TArr _, Seq(ss, x) -> typed (Seq(ss, ownIn local x)) e.t
    | TArr _, _ -> typed (Call("copy", [ e ])) e.t
    | _ -> e

let own = ownIn (fun _ -> false)

let rec tryConv (e: E) (ty: Ty) : E option =
    let a, b = resolve e.t, resolve ty
    if unify a b then Some e
    else
        match a, b with
        | TFn _, _ -> None // a block is not a value: no truthiness or text
        | _ when isScalar a && isScalar b -> Some(typed (Conv e) b)
        | _, TStr | _, TBool -> Some(typed (Conv e) b)
        | TArr _, TArr y when isScalar y -> // element by element
            let el = Desugar.fresh "_"
            let it = { kind = IMap; src = e; el = el; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do(mk (Call(castName y, [ mk (Var el) ]))) ]; post = [] }
            Some(typeE (mk (Iter it)))
        | _ -> None

and conv (e: E) (ty: Ty) : E =
    match tryConv e ty with
    | Some e -> e
    | None ->
        match resolve e.t with
        | TFn _ when resolve ty = TBool -> err "a block can't be used as bool; to filter or test, write the condition itself: x[_>2], not x[{_>2}]"
        | TFn _ -> err ($"a block can't be used as {showT ty}; to use its result, call it: f:x")
        | _ -> err ($"cannot convert {showVal e} to {showT ty}")

/// Blocks can't escape, so none can be put into an array (also catches a map whose body is a block: x{{..}}).
and stored (x: E) : E =
    match resolve x.t with
    | TFn _ -> err "a block can't be stored in an array"
    | _ -> x

/// Blocks can't escape: one can only be called, or passed to a function that takes a block.
and value (x: E) : E =
    match resolve x.t with
    | TFn _ -> err "a block isn't a value: call it, as in f:x"
    | _ -> x

// ---------- expressions ----------

and typeE (e: E) : E =
    if e.t <> TUnk then e
    else
        match posOf e with
        | Some(l, c) ->
            let saved = line, col
            line <- l
            col <- c
            try typeAt e finally (line <- fst saved; col <- snd saved)
        | None -> typeAt e

and typeAt (e: E) : E =
        match intercept |> Seq.tryPick (fun h -> h e) with
        | Some r -> r
        | None ->
        match e.k with
        | Int _ -> typed e.k TInt
        | Float _ -> typed e.k TFloat
        | Str _ -> typed e.k TStr
        | Bool _ -> typed e.k TBool
        | Call("i|", [ ix; i ]) -> typeE (if (lookup "i").IsSome then i else ix)
        | Var name ->
            match lookup name with
            | Some u ->
                // `x'` read before its first shift: it has x's type
                match resolve varTy.[u], Desugar.prevOf.TryGetValue name with
                | THole _ as h, (true, src) when (lookup src).IsSome -> unify h (typeE (mk (Var src))).t |> ignore
                | _ -> ()
                match resolve varTy.[u] with
                | TFn id when (match Desugar.arity.TryGetValue id with | true, 0 -> true | _ -> false) -> userCall id [] // bare `f` calls a block without parameters
                | t ->
                    posOf e |> Option.iter (fun (l, c) -> hovers.Add(l, c, name.Split([| '\''; '%' |]).[0], t))
                    typed (Var u) t
            | None when name = "true" || name = "false" -> err ($"undefined variable '{name}': booleans are T and F")
            | None ->
                let rec names (f: Frame) = Seq.append f.vars.Keys (match f.parent with Some p -> names p | None -> Seq.empty)
                err ($"undefined variable '{name}'" + didYouMean name (names cur))
        | Arr [] ->
            let h = newHole ()
            emptyEls.Add h
            typed (Arr []) (TArr h)
        | Arr xs ->
            let xs = List.map (typeE >> stored) xs
            let el = xs.Head.t
            typed (Arr(xs |> List.map (fun x -> own (conv x el)))) (TArr el)
        | Call("zero", []) ->
            // the zero value of a type known later (Desugar: `x'` before enough iterations); the runtime builds it
            let h = newHole ()
            let sg: Stdlib.Sig = { name = "zero"; parms = []; ret = Stdlib.SVar "T"; csym = "ns_zero"; tvars = [ "T" ]; rdesc = false }
            typed (StdCall(sg, [ typed Desc h ])) h
        | Bin(op, a, b) -> typeBin op (inPlace |> Option.exists (fun x -> obj.ReferenceEquals(x, e))) (typeE a) (typeE b)
        | Call("%own", [ x ]) -> typeE x // a fold's first element becomes the accumulator (a feature may copy it)
        | If(c, a, b) ->
            let c = conv (typeE c) TBool
            let a = value (typeE a)
            let saved = expect
            match resolve a.t with
            | THole _ -> ()
            | t -> expect <- Some t
            let b = value (typeE b)
            expect <- saved
            match tryConv b a.t with
            | Some b -> typed (If(c, a, b)) a.t
            | None ->
                // branches with unrelated types (e.g. two statement groups) run as a statement whose value is 0
                let asStmt (x: E) = typed (Seq([ Do x ], typed (Int 0L) TInt)) TInt
                typed (If(c, asStmt a, asStmt b)) TInt
        | Call(name, args) when name.StartsWith callMark || name.StartsWith nameMark || name.StartsWith negMark ->
            let f = name.Substring 1
            match lookup f with
            | Some u when (match resolve varTy.[u] with TFn _ -> false | _ -> true) ->
                if name.StartsWith nameMark then typeE { e with k = Var f }
                elif name.StartsWith negMark then typeE { e with k = Call("not", [ mk (Var f) ]) }
                else err ($"'{f.Split('\'').[0]}' is a variable here, not a function")
            | _ when name.StartsWith negMark -> typeE args.Head
            | _ -> typeE { e with k = Call(f, args) }
        | Call(name, args) ->
            match lookup name with
            | Some u when (match resolve varTy.[u] with TFn _ -> true | _ -> false) ->
                let (TFn id) = resolve varTy.[u]
                // as many arguments as the block reads, but one is always allowed: a block without `_` is called as `f:0`
                match Desugar.arity.TryGetValue id with
                | true, n when args.Length > max 1 n || args.Length < n -> err ($"'{name.Split('\'').[0]}' takes {n} argument(s) but was given {args.Length}")
                | _ -> ()
                userCall id (List.map (typeE >> own) args)
            | _ -> builtin name (List.map typeE args)
        | Index(x, i) -> typeIndex (typeE x) (typeE i)
        | Iter _ -> err "no feature types iteration"
        | Fn(id, body) ->
            fnDefs.[id] <- (body, cur)
            typed e.k (TFn id)
        | Seq(ss, x) ->
            let ss = typeStmts ss
            let x = typeE x
            typed (Seq(ss, x)) x.t
        | Conv _ -> err "unexpected conversion"

and typeBin op inPlace (a: E) (b: E) : E =
    let isArith = List.contains op [ "+"; "-"; "*"; "/"; "%"; "^^" ]
    let isCmp = List.contains op [ "="; "!="; "<"; ">"; "<="; ">=" ]
    let isBit = List.contains op [ "^"; "<<"; ">>"; ">>>" ]
    match resolve a.t, expect with
    | THole _, Some t -> unify a.t t |> ignore // a recursive call: its result type is the base case's
    | THole _, None when op = "++" ->
        unify a.t (match resolve b.t with TArr _ -> b.t | t -> TArr t) |> ignore
    | THole _, None -> unify a.t b.t |> ignore
    | _ -> ()
    let L = resolve a.t
    let same x = typed (Bin(op, a, conv b L)) L
    if isCmp then typed (Bin(op, a, conv b L)) TBool else
    match bins |> Seq.tryPick (fun h -> h op inPlace a b) with
    | Some r -> r
    | None ->
    match op, L with
    | ("&" | "|"), TInt -> same ()
    | _, TInt when isBit -> same ()
    | "^", TBool -> same ()
    | "&", _ -> typed (If(conv a TBool, conv b TBool, typed (Bool false) TBool)) TBool
    | "|", _ -> typed (If(conv a TBool, typed (Bool true) TBool, conv b TBool)) TBool
    | "**", (TInt | TFloat) -> err "** repeats arrays and strings; power is ^^"
    | _, (TInt | TFloat) when isArith ->
        match resolve b.t with
        | TArr _ -> err ($"{showT L} {op} {showT b.t}: put the array on the left")
        | _ -> same ()
    | ("+" | "-" | "*"), TBool -> same ()
    | _, TFn _ -> err ($"operator {op} not defined for a block; to call it, write f:x")
    | _ -> err ($"operator {op} not defined for {showVal a}")

and typeIndex (x: E) (i: E) =
    match indexers |> Seq.tryPick (fun h -> h x i) with
    | Some r -> r
    | None -> err ($"cannot index {showVal x}")

/// The element type of a collection (a map's keys), for primitive `name`'s error.
and elemOf (name: string) (x: E) =
    match resolve x.t with
    | TArr e | TCon(_, e :: _) -> e
    | TStr -> TStr
    | TMap(k, _) -> k
    | t -> err ($"""{(match name with "len" -> "#" | "has" -> "@?" | n -> n)}: not a collection: {showVal x}""")

and builtin name (args: E list) : E =
    let call t = typed (Call(name, args)) t
    let elemOf = elemOf name
    match (match prims.TryGetValue name with | true, hs -> hs |> Seq.tryPick (fun h -> h args) | _ -> None) with
    | Some r -> r
    | None ->
    match name, args with
    | ("print" | "println"), [ x ] -> typed (Call(name, [ value x ])) x.t
    | ("int" | "float" | "str" | "bool"), [ x ] ->
        conv x (match name with "int" -> TInt | "float" -> TFloat | "str" -> TStr | _ -> TBool)
    // compile-time type tests (blocks are monomorphised per call site, so the type is known)
    | ("isint" | "isfloat" | "isstr"), [ x ] ->
        typed (Bool(match name, resolve x.t with "isint", TInt | "isfloat", TFloat | "isstr", TStr -> true | _ -> false)) TBool
    | "neg", [ x ] ->
        match resolve x.t with
        | TInt | TFloat -> call x.t
        | _ -> typed (Call("neg", [ conv x TInt ])) TInt
    | "not", [ x ] -> typed (Call("not", [ conv x TBool ])) TBool
    | "notbool", [ x ] -> // `!{..}`: the block's value must be a bool
        if unify (resolve x.t) TBool then typed (Call("not", [ x ])) TBool
        else err ($"!{{..}} negates a block that must return bool, but this one returns {showT x.t}")
    | "len", [ x ] -> (elemOf x |> ignore; call TInt)
    | "has", [ c; x ] -> typed (Call("has", [ c; conv x (elemOf c) ])) TBool
    | "panic", [ msg ] -> call TInt
    | ("sum" | "prod"), [ x ] when (match resolve x.t with TArr e -> (match resolve e with TInt | TFloat | THole _ -> false | _ -> true) | _ -> false) ->
        let hint = match resolve x.t with TArr e when (match resolve e with TTup _ -> true | _ -> false) -> "; fold tuples with /+ or /*" | _ -> ""
        err ($"{name}: needs an array of int or float, not {showT x.t}{hint}")
    | ("any" | "all"), [ x ] when (match resolve x.t with TArr e -> (match resolve e with TBool | TInt | TFloat | THole _ -> false | _ -> true) | _ -> true) ->
        err ($"{name}: needs an array of bool, int or float, not {showT x.t}; map it first, e.g. xs{{_ > 0}}.{name}")
    | "destructure", [ x; { k = Int n } ] -> // `a, b = x`
        match resolve x.t with
        | TTup ts when ts.Length <> int n -> err ($"{n} names on the left but the tuple has {ts.Length} values")
        | TInt | TFloat | TBool | TFn _ as t -> err ($"can't destructure {showT t} into {n} names")
        | _ -> x
    | _ when Stdlib.names.Contains name -> stdCall name args
    | _ -> err ($"unknown function '{name}' with {args.Length} argument(s)" + didYouMean name Parser.builtins)

/// A runtime stdlib call, typed from its `//@` signature (see Stdlib.fs). Type variables bind on first
/// use; other arguments convert to the parameter type. The typed node carries one descriptor string per
/// type variable (and the result's, see Stdlib) before the arguments; a block argument becomes a `BlockRef`.
and stdCall name (args: E list) : E =
    let rec toTy (env: Dictionary<string, Ty>) (p: Stdlib.STy) : Ty =
        match p with
        | Stdlib.SInt -> TInt
        | Stdlib.SFloat -> TFloat
        | Stdlib.SStr -> TStr
        | Stdlib.SBool -> TBool
        | Stdlib.SVar v -> (match env.TryGetValue v with | true, t -> t | _ -> (let h = newHole () in env.[v] <- h; h))
        | Stdlib.SArr t -> TArr(toTy env t)
        | Stdlib.SMap(k, v) -> TMap(toTy env k, toTy env v)
        | Stdlib.SCon(n, t) -> TCon(n, [ toTy env t ])
        | Stdlib.STup ts -> TTup(List.map (toTy env) ts)
        | Stdlib.SFn _ -> err "nested block types are not supported"
    /// binds unbound variables from the actual type; true when no conversion is needed
    let rec bind (env: Dictionary<string, Ty>) (p: Stdlib.STy) (t: Ty) : bool =
        match p, resolve t with
        | Stdlib.SVar v, t ->
            match env.TryGetValue v with
            | true, b -> resolve b = t
            | _ -> (env.[v] <- t; true)
        | Stdlib.SArr p, TArr t -> bind env p t
        | Stdlib.SCon(n, p), TCon(m, t :: _) when n = m -> bind env p t
        | Stdlib.SMap(pk, pv), TMap(k, v) -> bind env pk k && bind env pv v
        | Stdlib.STup ps, TTup ts -> ps.Length = ts.Length && List.forall2 (bind env) ps ts
        | Stdlib.SFn _, TFn _ -> true
        | Stdlib.SInt, TInt | Stdlib.SFloat, TFloat | Stdlib.SStr, TStr | Stdlib.SBool, TBool -> true
        | _ -> false
    let candidates = Stdlib.overloads name |> List.filter (fun (sg: Stdlib.Sig) -> sg.parms.Length = args.Length)
    if candidates.IsEmpty then err ($"{name}: no overload takes {args.Length} argument(s)")
    // no overload fits: list them all, e.g. "max takes (T,T), [T] or BT T, not str"
    let mismatch () =
        let shows f xs = match xs with [ x ] -> f x | xs -> "(" + (xs |> List.map f |> String.concat ",") + ")"
        let alts = Stdlib.overloads name |> List.map (fun sg -> shows Stdlib.showSTy sg.parms) |> List.distinct |> List.rev
        let alts = if alts.Length = 1 then alts.Head else String.concat ", " (List.rev alts.Tail) + " or " + alts.Head
        $"{name} takes {alts}, not {shows (fun (a: E) -> showT a.t) args}"
    let exact (sg: Stdlib.Sig) = let env = Dictionary() in List.forall2 (bind env) sg.parms (args |> List.map (fun a -> a.t))
    let sg = candidates |> List.tryFind exact |> Option.defaultValue candidates.Head
    let env = Dictionary<string, Ty>()
    let args =
        List.map2 (fun (p: Stdlib.STy) (a: E) ->
            match p, resolve a.t with
            | Stdlib.SFn(pts, rt), TFn id ->
                // one tuple for a block that names `_1 _2 …` (or takes several parameters): its parts, as in a map block
                let id =
                    match pts with
                    | [ pt ] when (Desugar.namesParts.Contains id || Desugar.arity.[id] > 1) && not (noSpread |> Seq.exists (fun f -> f id)) ->
                        match resolve (toTy env pt) with TTup _ -> spreadBlock id | _ -> id
                    | _ -> id
                let call = userCall id (pts |> List.map (fun pt -> typed (Int 0L) (toTy env pt)))
                match call.k, rt with
                | UserCall(inst, _), Stdlib.SBool when unify call.t TBool -> typed (BlockRef inst) a.t
                | UserCall(inst, _), Stdlib.SVar v ->
                    env.[v] <- call.t
                    typed (BlockRef inst) a.t
                | _ -> err ($"{name}: the block must return {Stdlib.showSTy rt}")
            | Stdlib.SFn _, _ -> err ($"{name} expects a block argument")
            | Stdlib.SCon("BT", _), TCon("BT", _) -> (bind env p a.t |> ignore; a) // keeps its key block
            | p, _ ->
                let a = value a
                bind env p a.t |> ignore
                let a = match tryConv a (toTy env p) with Some a -> a | None -> err (mismatch ())
                match a.k with Var _ -> own a | _ -> a) sg.parms args // an element argument is only read
    // descriptors are rendered by codegen, once every hole is resolved
    let descs = sg.tvars |> List.map (fun v -> typed Desc (toTy env (Stdlib.SVar v)))
    let ret = toTy env sg.ret
    let rdesc = if sg.rdesc then [ typed Desc ret ] else []
    typed (StdCall(sg, descs @ rdesc @ args)) ret

/// A copy of block `id` taking one tuple, `_k` reading its member k-1 (nested blocks keep their own `_k`).
and spreadBlock id =
    match spreads.TryGetValue id with
    | true, w -> w
    | _ ->
        let body, fr = fnDefs.[id]
        let rec e (x: E) =
            match x.k with
            | Var n when n.Length > 1 && n.[0] = '_' && Seq.forall System.Char.IsDigit n.[1..] ->
                Desugar.rebuild x (Index(mk (Var "_1"), mk (Int(int64 n.[1..] - 1L))))
            | Fn _ -> x
            | _ -> Desugar.mapE e ss x
        and ss body = List.map (Desugar.mapS e ss) body
        let w = Desugar.freshId ()
        fnDefs.[w] <- (ss body, fr)
        Desugar.arity.[w] <- 1
        spreads.[id] <- w
        w

and userCall id (args: E list) : E =
    let tys = args |> List.map (fun a -> resolve a.t)
    let body, defFr = fnDefs.[id]
    // a block nested in a function belongs to that function's instance: it captures that instance's variables
    let outer = match defFr.inst with Some i -> i.name + "/" | None -> ""
    let prefix = outer + string id
    let key = prefix + "(" + (tys |> List.map showTy |> String.concat ",") + ")"
    let unfixed (h: Ty) = match resolve h with THole _ -> true | _ -> false
    let twoTypes () =
        err "a block that fixes the element type of an empty literal ([] %[] [->]) from outside it can't be called at two different types; give the literal its type first"
    let inst =
        match insts.TryGetValue key with
        | true, i -> i
        | _ ->
            if fixers.Contains prefix then twoTypes ()
            // an empty literal passed as an argument belongs to the call, not to the block
            let before = emptyEls |> Seq.filter (fun h -> unfixed h && not (List.exists (holeIn h) tys)) |> List.ofSeq
            let inst =
                { name = $"f{id}_{instList.Count}"
                  parms = []
                  caps = []
                  ret = newHole ()
                  body = []
                  result = typed (Int 0L) TInt
                  owned = HashSet()
                  calls = HashSet()
                  borrowed = HashSet() }
            let fr = { parent = Some defFr; vars = Dictionary(); inst = Some inst }
            inst.parms <- tys |> List.mapi (fun i t -> declare fr ($"_{i + 1}") t, t)
            insts.[key] <- inst
            instList.Add inst
            let saved, savedLine, savedCol = cur, line, col
            cur <- fr
            let typeBody body =
                try typeStmts (bodyRewrites |> Seq.fold (fun b f -> f id tys.Length b) body)
                with CompileError(l, c, msg) when not (msg.Contains "(when called at line ") ->
                    // the body is fine in general; say which call's argument types broke it
                    let ats = if tys.IsEmpty then "" else " with " + (tys |> List.map showT |> String.concat ", ")
                    raise (CompileError(l, c, $"{msg} (when called at line {showLine savedLine}{ats})"))
            let ss = typeBody body
            // a body ending in a map over its own recursive calls has no finite type ([[[..]]]): the function is
            // run for its effects, so that map becomes an `each` and the function returns nothing (0)
            let ss =
                match List.rev ss, List.rev body with
                | Do r :: _, Do({ k = Iter it } as e) :: rest when it.kind = IMap && unfixed inst.ret && holeIn inst.ret r.t ->
                    inst.owned.Clear(); inst.calls.Clear(); inst.caps <- []
                    inst.parms <- tys |> List.mapi (fun i t -> declare fr ($"_{i + 1}") t, t)
                    typeBody (List.rev (Do { e with k = Iter { it with kind = IEach } } :: rest))
                | _ -> ss
            match List.rev ss with
            | Do r :: _ when unfixed inst.ret && holeIn inst.ret r.t ->
                err "this function's result contains its own result ([[..]] without end); give it a base case of a fixed type first, or end it in a map of the recursive calls to run it for effect"
            | Do r :: rest ->
                // the result is fresh unless it is shared: a dying local is handed over, anything else is copied
                inst.result <- ownIn inst.owned.Contains (conv (value r) inst.ret)
                inst.body <- List.rev rest
            | _ -> err "function body has no value"
            cur <- saved
            line <- savedLine
            col <- savedCol
            // a lazy function's sink version is keyed by its sink, the block that fixes the consumer's result
            if before |> List.exists (unfixed >> not) && not (fixerExempt |> Seq.exists (fun f -> f id)) then
                if insts.Keys |> Seq.exists (fun k -> k <> key && k.StartsWith(prefix + "(")) then twoTypes ()
                fixers.Add prefix |> ignore
            inst
    cur.inst |> Option.iter (fun c -> c.calls.Add inst.name |> ignore)
    typed (UserCall(inst.name, args)) inst.ret

// ---------- statements ----------

and typeStmt (s: S) : S =
    match s with
    | Line n -> (if n <> line then (line <- n; col <- 0)); s
    | Do e -> Do(typeE e)
    | While(c, body) ->
        let c = conv (typeE c) TBool
        While(c, typeStmts body)
    | SetIdx(x, i, value) ->
        let x = typeE x
        let i, value = typeE i, typeE value
        match setIndexers |> Seq.tryPick (fun h -> h x i value) with
        | Some s -> s
        | None -> err ($"cannot assign into {showVal x}")
    | Assign(name, e0) ->
        let rec self (x: E) =
            match x.k with
            | Bin(_, { k = Var n }, _) when n = name -> Some x
            | Seq(ss, y) when ss |> List.forall (function Line _ -> true | _ -> false) -> self y
            | _ -> None
        let saved = inPlace
        inPlace <- self e0
        let e = typeE e0
        inPlace <- saved
        let generated = name.Contains '%'
        if not generated then posOf e0 |> Option.iter (fun (l, _) -> hovers.Add(l, 0, name.Split('\'').[0], e.t))
        let store u (e: E) =
            if generated then freshTemps.[u] <- (match freshTemps.TryGetValue u with | true, f -> f | _ -> true) && fresh e
            Assign(u, if generated then e else own e)
        match lookup name with
        | Some u ->
            match assignRewrites |> Seq.tryPick (fun f -> f u e) with
            | Some s -> s
            | None ->
                match resolve varTy.[u], resolve e.t with
                | TFn a, TFn b when a <> b -> err "a function variable cannot be reassigned to another block"
                | TFn _, _ | _, TFn _ -> store u (conv e varTy.[u])
                | t, et ->
                    match tryConv e t with
                    | Some e -> store u e
                    | None when generated -> store u (conv e t)
                    | None ->
                        // a variable keeps the type of its first assignment; point at the new value
                        posOf e0 |> Option.iter (fun (l, c) -> line <- l; col <- c)
                        err ($"'{name.Split('\'').[0]}' holds {showT t} (from its first assignment), so it can't be assigned {showT et}")
        | None -> store (declare cur name e.t) e

and typeStmts (ss: S list) =
    // a map whose result is discarded (not the block's last statement) runs as `each`: no result array
    let n = List.length ss
    ss |> List.mapi (fun i s ->
        match s with
        | Do({ k = Iter it } as e) when i < n - 1 && it.kind = IMap -> typeStmt (Do { e with k = Iter { it with kind = IEach } })
        | _ -> typeStmt s)

// ---------- program ----------

/// Callers must forward the captures of the functions they call.
let private propagateCaptures () =
    let byName = instList |> Seq.map (fun i -> i.name, i) |> dict
    let mutable changed = true
    while changed do
        changed <- false
        for i in instList do
            for c in i.calls do
                for cap in byName.[c].caps do
                    if not (i.owned.Contains cap) && not (List.contains cap i.caps) then
                        i.caps <- i.caps @ [ cap ]
                        changed <- true

let check (prog: S list) : Program =
    hovers.Clear(); varTy.Clear(); globals.Clear(); fnDefs.Clear(); spreads.Clear(); insts.Clear(); instList.Clear(); global0.vars.Clear(); freshTemps.Clear()
    emptyEls.Clear(); fixers.Clear()
    for r in resets do r ()
    cur <- global0
    let main = typeStmts prog
    let main = (prologue |> Seq.collect (fun f -> f ()) |> List.ofSeq) @ main
    propagateCaptures ()
    { main = main
      globals = globals |> Seq.map (fun g -> g, varTy.[g]) |> List.ofSeq
      insts = List.ofSeq instList
      varTy = varTy }
