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
      calls: HashSet<string> }

type Program =
    { main: S list
      globals: (string * Ty) list
      insts: Inst list
      varTy: Dictionary<string, Ty> }

let private varTy = Dictionary<string, Ty>()
let private globals = ResizeArray<string>()
let private fnDefs = Dictionary<int, S list * Frame>()
let private insts = Dictionary<string, Inst>()
let private instList = ResizeArray<Inst>()
let private global0 = { parent = None; vars = Dictionary(); inst = None }
let mutable private cur = global0
let mutable private line = 0
/// Type of the then-branch while typing an else-branch; resolves `rec + rec` in recursive functions.
let mutable private expect: Ty option = None
let mutable private counter = 0
let private err msg = fail line msg

let newHole () = THole(ref None)
let private typed k t = { k = k; t = t }
let private isScalar t = match resolve t with TInt | TFloat | TStr | TBool -> true | _ -> false

/// Unifies two types, binding holes; when they don't unify, no hole stays bound.
let private unify a b =
    let bound = ResizeArray<Ty option ref>()
    let rec go a b =
        match resolve a, resolve b with
        | THole r1, THole r2 when obj.ReferenceEquals(r1, r2) -> true
        | THole r, t | t, THole r -> (r.Value <- Some t; bound.Add r; true)
        | TArr x, TArr y | TSet x, TSet y | TQ x, TQ y | TPQ x, TPQ y | TGraph x, TGraph y -> go x y
        | TMap(k1, v1), TMap(k2, v2) -> go k1 k2 && go v1 v2
        | TTup xs, TTup ys -> xs.Length = ys.Length && List.forall2 go xs ys
        | x, y -> x = y
    if go a b then true
    else
        for r in bound do r.Value <- None
        false

// ---------- scopes ----------

let private declare (fr: Frame) (name: string) (ty: Ty) =
    counter <- counter + 1
    let u = $"{name}.{counter}"
    fr.vars.[name] <- u
    varTy.[u] <- ty
    match fr.inst with
    | Some i -> i.owned.Add u |> ignore
    | None -> globals.Add u
    u

/// Finds a variable lexically; a hit in an enclosing function frame becomes a capture of every frame in between.
let private lookup (name: string) =
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

let private castName t =
    match resolve t with
    | TInt -> "int"
    | TFloat -> "float"
    | TStr -> "str"
    | TBool -> "bool"
    | t -> err ($"no conversion to {showTy t}")

/// Arrays are values: copy when a variable's array is stored somewhere else.
let private own (e: E) =
    match e.k, resolve e.t with
    | Var _, TArr _ -> typed (Call("copy", [ e ])) e.t
    | _ -> e

/// Is `x` an edge (u,v) or (u,v,weight) of a graph with node type n, rather than a node? Unifies the members.
let private isEdge (n: Ty) (x: E) =
    let isNode = match resolve n with THole _ -> false | _ -> unify x.t n
    not isNode
    && match resolve x.t with
       | TTup(_ :: _ :: w) when w.Length <= 1 -> unify x.t (TTup([ n; n ] @ List.map (fun _ -> TInt) w))
       | _ -> false

let rec private tryConv (e: E) (ty: Ty) : E option =
    let a, b = resolve e.t, resolve ty
    if unify a b then Some e
    else
        match a, b with
        | _ when isScalar a && isScalar b -> Some(typed (Conv e) b)
        | _, TStr | _, TBool -> Some(typed (Conv e) b)
        | TArr _, TArr y when isScalar y -> Some(typeE (Lower.convArr e (castName y)))
        | _ -> None

and private conv (e: E) (ty: Ty) : E =
    match tryConv e ty with
    | Some e -> e
    | None -> err ($"cannot convert {showTy e.t} to {showTy ty}")

// ---------- expressions ----------

and typeE (e: E) : E =
    if e.t <> TUnk then e
    else
        match e.k with
        | Int _ -> typed e.k TInt
        | Float _ -> typed e.k TFloat
        | Str _ -> typed e.k TStr
        | Bool _ -> typed e.k TBool
        | Var name ->
            match lookup name with
            | Some u -> typed (Var u) varTy.[u]
            | None -> err ($"undefined variable '{name}'")
        | Arr [] -> typed (Arr []) (TArr(newHole ()))
        | Arr xs ->
            let xs = List.map typeE xs
            let el = xs.Head.t
            typed (Arr(xs |> List.map (fun x -> own (conv x el)))) (TArr el)
        | Bin(op, a, b) -> typeBin op (typeE a) (typeE b)
        | If(c, a, b) ->
            let c = conv (typeE c) TBool
            let a = typeE a
            let saved = expect
            match resolve a.t with
            | THole _ -> ()
            | t -> expect <- Some t
            let b = typeE b
            expect <- saved
            match tryConv b a.t with
            | Some b -> typed (If(c, a, b)) a.t
            | None ->
                // branches with unrelated types (e.g. two statement groups) run as a statement whose value is 0
                let asStmt (x: E) = typed (Seq([ Do x ], typed (Int 0L) TInt)) TInt
                typed (If(c, asStmt a, asStmt b)) TInt
        | Call(name, args) ->
            match lookup name with
            | Some u when (match resolve varTy.[u] with TFn _ -> true | _ -> false) ->
                let (TFn id) = resolve varTy.[u]
                userCall id (List.map (typeE >> own) args)
            | _ -> builtin name (List.map typeE args)
        | Index(x, i) -> typeIndex (typeE x) (typeE i)
        | Iter it ->
            // collect a chain of map/filter stages feeding this one (innermost first)
            let rec chain (it: Iter) acc =
                match it.src.k with
                | Iter inner when it.src.t = TUnk && (inner.kind = IMap || inner.kind = IFilter) -> chain inner (it :: acc)
                | _ -> it :: acc
            let stages = chain it []
            let src = typeE stages.Head.src
            match resolve src.t with
            | TArr _ | TStr when stages.Length > 1 ->
                typeE (Lower.iter ({ stages.Head with src = src } :: stages.Tail) (resolve src.t))
            | _ ->
                // not fusable (maps/sets/queues keep their kind per stage): lower stage by stage
                stages |> List.fold (fun (s: E) st -> typeE (Lower.iter [ { st with src = s } ] (resolve s.t))) src
        | Fn(id, body) ->
            fnDefs.[id] <- (body, cur)
            typed e.k (TFn id)
        | Seq(ss, x) ->
            let ss = typeStmts ss
            let x = typeE x
            typed (Seq(ss, x)) x.t
        | MatchOn(src, t, arms, filter) ->
            let src = typeE src
            match resolve src.t with
            | TArr _ ->
                let it = { kind = (if filter then IFilter else IMap); src = src; el = t; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do arms ] }
                typeE (mk (Iter it))
            | t when filter -> err ($"?[ ] filters an array, not {showTy t}")
            | _ -> typeE (mk (Seq([ Assign(t, src) ], arms)))
        | Conv _ -> err "unexpected conversion"

and private typeBin op (a: E) (b: E) : E =
    let isArith = List.contains op [ "+"; "-"; "*"; "/"; "%"; "^^" ]
    let isCmp = List.contains op [ "="; "!="; "<"; ">"; "<="; ">=" ]
    let isBit = List.contains op [ "^"; "<<"; ">>"; ">>>" ]
    match resolve a.t with
    | THole _ when op = "++" ->
        unify a.t (match resolve b.t with TArr _ -> b.t | t -> TArr t) |> ignore
    | THole _ ->
        unify a.t b.t |> ignore
        match resolve a.t, expect with
        | THole _, Some t -> unify a.t t |> ignore
        | _ -> ()
    | _ -> ()
    let L = resolve a.t
    let same x = typed (Bin(op, a, conv b L)) L
    let sameKind = match L, resolve b.t with TSet _, TSet _ | TGraph _, TGraph _ -> true | _ -> false
    match op, L with
    | _ when isCmp -> typed (Bin(op, a, conv b L)) TBool
    | ("&" | "|" | "+" | "-"), TSet _ | ("&" | "|" | "-"), TGraph _ when sameKind ->
        stdCall (match op with "&" -> "ginter" | "-" -> "gdiff" | _ -> "gunion") [ a; conv b L ]
    | ("&" | "|"), TInt -> same ()
    | _, TInt when isBit -> same ()
    | "^", TBool -> same ()
    | "&", _ -> typed (If(conv a TBool, conv b TBool, typed (Bool false) TBool)) TBool
    | "|", _ -> typed (If(conv a TBool, typed (Bool true) TBool, conv b TBool)) TBool
    | "++", TArr el ->
        match resolve b.t with
        | TArr _ -> typed (Call("concat", [ a; conv b L ])) L
        | _ -> typed (Call("append", [ a; own (conv b el) ])) L
    | "+", TGraph n when isEdge n b -> stdCall "addedge" [ a; b ]
    | "-", TGraph n -> if isEdge n b then stdCall "deledge" [ a; b ] else stdCall "delnode" [ a; b ]
    | "+", (TSet el | TQ el | TPQ el) -> typed (Call("add", [ a; own (conv b el) ])) L
    | "-", TSet el -> typed (Call("del", [ a; conv b el ])) L
    | "+", TMap _ -> typed (Call("merge", [ a; conv b L ])) L
    | "-", TMap(k, _) -> typed (Call("del", [ a; conv b k ])) L
    | _, TArr _ when isArith || isBit ->
        let bIsArr = match resolve b.t with TArr _ -> true | _ -> false
        typeE (Lower.broadcast op a b bIsArr)
    | _, (TInt | TFloat) when isArith ->
        match resolve b.t with
        | TArr _ -> err "scalar op array: put the array on the left"
        | _ -> same ()
    | "+", TStr -> same ()
    | "*", TStr -> typed (Bin("*", a, conv b TInt)) TStr
    | ("+" | "-" | "*"), TBool -> same ()
    | _ -> err ($"operator {op} not defined for {showTy L}")

and private typeIndex (x: E) (i: E) =
    match resolve x.t with
    | TArr el -> typed (Index(x, conv i TInt)) el
    | TStr -> typed (Index(x, conv i TInt)) TStr
    | TMap(k, v) -> typed (Index(x, conv i k)) v
    | TGraph n -> if isEdge n i then stdCall "weight" [ x; i ] else stdCall "nbrs" [ x; i ]
    | TQ el | TPQ el ->
        match i.k with
        | Int 0L -> typed (Call("peek", [ x ])) el
        | _ -> typed (Index(stdCall "arr" [ x ], conv i TInt)) el
    | TTup ts ->
        match i.k with
        | Int k when abs (int k) < ts.Length + (if k < 0L then 1 else 0) ->
            let k = if k < 0L then int k + ts.Length else int k
            typed (Call("tget", [ x; typed (Int(int64 k)) TInt ])) ts.[k]
        | _ -> err ($"tuple index must be a literal 0..{ts.Length - 1}")
    | t -> err ($"cannot index {showTy t}")

and private builtin name (args: E list) : E =
    let call t = typed (Call(name, args)) t
    // a range end that is an array means its length: 0..xs = 0..#xs
    let bound (x: E) = match resolve x.t with TArr _ -> typed (Call("len", [ x ])) TInt | _ -> conv x TInt
    let elemOf (x: E) =
        match resolve x.t with
        | TArr e | TSet e | TQ e | TPQ e -> e
        | TStr -> TStr
        | TMap(k, _) | TGraph k -> k
        | t -> err ($"{name}: not a collection: {showTy t}")
    match name, args with
    | ("print" | "println"), [ x ] -> typed (Call(name, [ x ])) x.t
    | ("int" | "float" | "str" | "bool"), [ x ] ->
        conv x (match name with "int" -> TInt | "float" -> TFloat | "str" -> TStr | _ -> TBool)
    | "neg", [ x ] ->
        match resolve x.t with
        | TInt | TFloat -> call x.t
        | TArr _ -> typeE (Lower.broadcast "*" x (typed (Int -1L) TInt) false)
        | TGraph _ -> stdCall "rev" [ x ]
        | _ -> typed (Call("neg", [ conv x TInt ])) TInt
    | "not", [ x ] -> typed (Call("not", [ conv x TBool ])) TBool
    | "len", [ x ] -> (elemOf x |> ignore; call TInt)
    | "has", [ c; x ] when (match resolve c.t with TGraph n -> isEdge n x | _ -> false) -> stdCall "hasedge" [ c; x ]
    | "has", [ c; x ] -> typed (Call("has", [ c; conv x (elemOf c) ])) TBool
    | "range", [ a; b ] -> typed (Call("range", [ bound a; bound b ])) (TArr TInt)
    | "slice", [ x; a; b ] ->
        match resolve x.t with
        | TArr _ | TStr -> typed (Call("slice", [ x; bound a; bound b ])) x.t
        | t -> err ($"cannot slice {showTy t}")
    | "mapof", [ ks; vs ] -> call (TMap(elemOf ks, elemOf vs))
    | ("set" | "Q" | "PQ"), [ xs ] ->
        let el = elemOf xs
        let t = match name with "set" -> TSet el | "Q" -> TQ el | _ -> TPQ el
        typed (Call(name, [ (match resolve xs.t with TArr _ -> xs | _ -> stdCall "arr" [ xs ]) ])) t
    | "push", [ a; x ] ->
        match resolve a.t with
        | TArr el -> typed (Call("push", [ a; own (conv x el) ])) a.t
        | t -> err ($"push: not an array: {showTy t}")
    | "panic", [ msg ] -> call TInt
    | "tuple", xs when xs.Length <= 9 -> typed (Call("tuple", List.map own xs)) (TTup(xs |> List.map (fun x -> x.t)))
    | "repeat", [ x; n ] -> typed (Call("repeat", [ own x; conv n TInt ])) (TArr x.t)
    | _ when Stdlib.names.Contains name -> stdCall name args
    | _ -> err ($"unknown function '{name}' with {args.Length} argument(s)")

/// A runtime stdlib call, typed from its `//@` signature (see Stdlib.fs). Type variables bind on first
/// use; other arguments convert to the parameter type. The typed node carries one descriptor string per
/// type variable (and the result's, see Stdlib) before the arguments; a block argument becomes a `BlockRef`.
and private stdCall name (args: E list) : E =
    let rec toTy (env: Dictionary<string, Ty>) (p: Stdlib.STy) : Ty =
        match p with
        | Stdlib.SInt -> TInt
        | Stdlib.SFloat -> TFloat
        | Stdlib.SStr -> TStr
        | Stdlib.SBool -> TBool
        | Stdlib.SVar v -> (match env.TryGetValue v with | true, t -> t | _ -> (let h = newHole () in env.[v] <- h; h))
        | Stdlib.SArr t -> TArr(toTy env t)
        | Stdlib.SGraph t -> TGraph(toTy env t)
        | Stdlib.SMap(k, v) -> TMap(toTy env k, toTy env v)
        | Stdlib.SSet t -> TSet(toTy env t)
        | Stdlib.SQ t -> TQ(toTy env t)
        | Stdlib.SPQ t -> TPQ(toTy env t)
        | Stdlib.STup ts -> TTup(List.map (toTy env) ts)
        | Stdlib.SFn _ -> err "nested block types are not supported"
    /// binds unbound variables from the actual type; true when no conversion is needed
    let rec bind (env: Dictionary<string, Ty>) (p: Stdlib.STy) (t: Ty) : bool =
        match p, resolve t with
        | Stdlib.SVar v, t ->
            match env.TryGetValue v with
            | true, b -> resolve b = t
            | _ -> (env.[v] <- t; true)
        | Stdlib.SArr p, TArr t | Stdlib.SGraph p, TGraph t | Stdlib.SSet p, TSet t | Stdlib.SQ p, TQ t | Stdlib.SPQ p, TPQ t -> bind env p t
        | Stdlib.SMap(pk, pv), TMap(k, v) -> bind env pk k && bind env pv v
        | Stdlib.STup ps, TTup ts -> ps.Length = ts.Length && List.forall2 (bind env) ps ts
        | Stdlib.SFn _, TFn _ -> true
        | Stdlib.SInt, TInt | Stdlib.SFloat, TFloat | Stdlib.SStr, TStr | Stdlib.SBool, TBool -> true
        | _ -> false
    let candidates = Stdlib.overloads name |> List.filter (fun (sg: Stdlib.Sig) -> sg.parms.Length = args.Length)
    if candidates.IsEmpty then err ($"{name}: no overload takes {args.Length} argument(s)")
    let exact (sg: Stdlib.Sig) = let env = Dictionary() in List.forall2 (bind env) sg.parms (args |> List.map (fun a -> a.t))
    let sg = candidates |> List.tryFind exact |> Option.defaultValue candidates.Head
    let env = Dictionary<string, Ty>()
    let args =
        List.map2 (fun (p: Stdlib.STy) (a: E) ->
            match p, resolve a.t with
            | Stdlib.SFn(pt, rt), TFn id ->
                let call = userCall id [ typed (Int 0L) (toTy env pt) ]
                match call.k, rt with
                | UserCall(inst, _), Stdlib.SBool when unify call.t TBool -> typed (BlockRef inst) a.t
                | UserCall(inst, _), Stdlib.SVar v ->
                    env.[v] <- call.t
                    typed (BlockRef inst) a.t
                | _ -> err ($"{name}: the block must return {Stdlib.showSTy rt}")
            | Stdlib.SFn _, _ -> err ($"{name} expects a block argument")
            | p, _ ->
                bind env p a.t |> ignore
                own (conv a (toTy env p))) sg.parms args
    // descriptors are rendered by codegen, once every hole is resolved
    let descs = sg.tvars |> List.map (fun v -> typed Desc (toTy env (Stdlib.SVar v)))
    let ret = toTy env sg.ret
    let rdesc = if sg.rdesc then [ typed Desc ret ] else []
    typed (StdCall(sg, descs @ rdesc @ args)) ret

and private userCall id (args: E list) : E =
    let tys = args |> List.map (fun a -> resolve a.t)
    let key = string id + "(" + (tys |> List.map showTy |> String.concat ",") + ")"
    let inst =
        match insts.TryGetValue key with
        | true, i -> i
        | _ ->
            let body, defFr = fnDefs.[id]
            let inst =
                { name = $"f{id}_{instList.Count}"
                  parms = []
                  caps = []
                  ret = newHole ()
                  body = []
                  result = typed (Int 0L) TInt
                  owned = HashSet()
                  calls = HashSet() }
            let fr = { parent = Some defFr; vars = Dictionary(); inst = Some inst }
            inst.parms <- tys |> List.mapi (fun i t -> declare fr ($"_{i + 1}") t, t)
            insts.[key] <- inst
            instList.Add inst
            let saved, savedLine = cur, line
            cur <- fr
            let ss = typeStmts body
            match List.rev ss with
            | Do r :: rest ->
                inst.result <- conv r inst.ret
                inst.body <- List.rev rest
            | _ -> err "function body has no value"
            cur <- saved
            line <- savedLine
            inst
    cur.inst |> Option.iter (fun c -> c.calls.Add inst.name |> ignore)
    typed (UserCall(inst.name, args)) inst.ret

// ---------- statements ----------

and private typeStmt (s: S) : S =
    match s with
    | Line n -> (line <- n; s)
    | Do e -> Do(typeE e)
    | While(c, body) ->
        let c = conv (typeE c) TBool
        While(c, typeStmts body)
    | SetIdx(x, i, value) ->
        let x = typeE x
        let i, value = typeE i, typeE value
        match resolve x.t with
        | TArr el -> SetIdx(x, conv i TInt, own (conv value el))
        | TMap(k, v) -> SetIdx(x, conv i k, own (conv value v))
        | t -> err ($"cannot assign into {showTy t}")
    | Assign(name, e) ->
        let e = typeE e
        let generated = name.Contains '%'
        let store u (e: E) = Assign(u, if generated then e else own e)
        match lookup name with
        | Some u ->
            match e.k with
            | Call("append", [ { k = Var src } as a; x ]) when src = u -> Do(typed (Call("push", [ a; x ])) e.t)
            | _ ->
                match resolve varTy.[u], resolve e.t with
                | TFn a, TFn b when a <> b -> err "a function variable cannot be reassigned to another block"
                | _ -> store u (conv e varTy.[u])
        | None -> store (declare cur name e.t) e

and private typeStmts (ss: S list) =
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
    varTy.Clear(); globals.Clear(); fnDefs.Clear(); insts.Clear(); instList.Clear(); global0.vars.Clear()
    cur <- global0
    let main = typeStmts prog
    propagateCaptures ()
    { main = main
      globals = globals |> Seq.map (fun g -> g, varTy.[g]) |> List.ofSeq
      insts = List.ofSeq instList
      varTy = varTy }
