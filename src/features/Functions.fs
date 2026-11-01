/// Functions: definitions `f a b = …` with base cases `f 0 = …` and modifiers (`memo`, `lazy`); block literals, calls to blocks (`f:x`, a bare `f` of a block without parameters,
/// a block passed to a stdlib function), and monomorphisation: each block is typed and compiled once per distinct
/// argument-type list (an instance, `Typer.Inst`), nested in the instance that defines it so it captures that
/// instance's variables. Codegen: user calls, the C-callable wrappers stdlib callbacks go through, and every
/// instance's definition. Scopes and the captures a lookup implies are the kernel's (Typer.lookup).
module Features.Functions

open System.Collections.Generic
open Ast
open Core
open Typer

/// Block id -> its body and the frame it was defined in.
let fnDefs = Dictionary<int, S list * Frame>()
let private spreads = Dictionary<int, int>() // block id -> its tuple-taking copy (spreadBlock)
let private insts = Dictionary<string, Inst>()
let private instList = ResizeArray<Inst>()
/// Blocks (instance key prefix) whose call fixed the element type of an empty literal from outside.
let private fixers = HashSet<string>()

// ---------- typing ----------

/// A copy of block `id` taking one tuple, `_k` reading its member k-1 (nested blocks keep their own `_k`).
let spreadBlock id =
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

let rec userCall id (args: E list) : E =
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

let private blockId (name: string) =
    match lookup name with
    | Some u -> (match resolve varTy.[u] with TFn id -> Some id | _ -> None)
    | None -> None

let private intercept (e: E) =
    match e.k with
    | Fn(id, body) ->
        fnDefs.[id] <- (body, cur)
        Some(typed e.k (TFn id))
    // bare `f` calls a block without parameters
    | Var name ->
        match blockId name with
        | Some id when (match Desugar.arity.TryGetValue id with | true, 0 -> true | _ -> false) -> Some(userCall id [])
        | _ -> None
    | Call(name, args) when not (name.StartsWith callMark || name.StartsWith nameMark || name.StartsWith negMark) ->
        match blockId name with
        | Some id ->
            // as many arguments as the block reads, but one is always allowed: a block without `_` is called as `f:0`
            match Desugar.arity.TryGetValue id with
            | true, n when args.Length > max 1 n || args.Length < n -> err $"'{name.Split(''').[0]}' takes {n} argument(s) but was given {args.Length}"
            | _ -> ()
            Some(userCall id (List.map (typeE >> own) args))
        | None -> None
    | _ -> None

/// A block given to a stdlib function: one tuple for a block that names `_1 _2 …` (or takes several parameters)
/// is its parts, as in a map block.
let private blockCall id (pts: Ty list) =
    let id =
        match pts with
        | [ pt ] when (Desugar.namesParts.Contains id || Desugar.arity.[id] > 1) && not (noSpread |> Seq.exists (fun f -> f id)) ->
            match resolve pt with TTup _ -> spreadBlock id | _ -> id
        | _ -> id
    userCall id (pts |> List.map (fun pt -> typed (Int 0L) pt))

// ---------- parsing: `f a b = …`, base cases `f 0 [a, b] = …`, modifiers `memo f a = …` ----------

type private Pat =
    | PName of string
    | PLit of Expr
    | PGroup of bool * Pat list // is an array, items

/// Per parser (a file or an interpolation): base cases (`f 0 [a, b] = a`) waiting for f's general definition:
/// arity, guard statements, a modifier on one of them.
let private clausesOf = System.Runtime.CompilerServices.ConditionalWeakTable<Parser.Parser, Dictionary<string, int * Stmt list * string option>>()
let private clauses (p: Parser.Parser) = clausesOf.GetValue(p, fun _ -> Dictionary())

/// `name param {param} =` starts a function definition: Some(name, params, index of `=`).
/// A param is a name or `(a, b, …)`, which unpacks a tuple or array argument (`_` skips an element).
let private fnDefAhead (p: Parser.Parser) =
    let name j = match p.Tok j with ID n when Parser.assignable n -> Some n | _ -> None
    let rec group j acc =
        let name j = if p.Tok j = ID "_" then Some "_" else name j // `_` skips an element
        match name j, p.Tok(j + 1) with
        | Some n, SYM "," -> group (j + 2) (n :: acc)
        | Some n, SYM ")" -> Some(List.rev (n :: acc), j + 2)
        | _ -> None
    let param j =
        match name j with
        | Some n -> Some([ n ], j + 1)
        | None when p.Tok j = SYM "(" -> group (j + 1) []
        | None -> None
    let rec pars j acc =
        match param j with
        | Some(ps, j) -> pars j (ps :: acc)
        | None -> List.rev acc, j
    match name p.Pos with
    | Some f when not (Parser.foreign.ContainsKey f) ->
        match pars (p.Pos + 1) [] with
        | ps, j when not ps.IsEmpty && p.Tok j = SYM "=" -> Some(f, ps, j)
        | _ -> None
    | _ -> None

/// `name pattern {pattern} =` with a literal somewhere: a base case of f (`f 0 (1, x) [a, b] = a + x`).
/// A pattern is a name, `_`, a literal, `(p, …)` (a tuple) or `[p, …]` (an array of exactly that length).
let private clauseAhead (p: Parser.Parser) : (string * Pat list * int) option =
    let rec pat j =
        match p.Tok j with
        | ID n when n = "_" || Parser.assignable n -> Some(PName n, j + 1)
        | INT n -> Some(PLit(EInt n), j + 1)
        | FLOAT x -> Some(PLit(EFloat x), j + 1)
        | BOOL b -> Some(PLit(EBool b), j + 1)
        | STR [] -> Some(PLit(EStr ""), j + 1)
        | STR [ Lit s ] -> Some(PLit(EStr s), j + 1)
        | SYM "-" -> (match p.Tok(j + 1) with INT n -> Some(PLit(EInt -n), j + 2) | FLOAT x -> Some(PLit(EFloat -x), j + 2) | _ -> None)
        | SYM("(" | "[" | "f[" as o) ->
            let close = if o = "(" then ")" else "]"
            let rec items j acc =
                if p.Tok j = SYM close && (o <> "(" || not (List.isEmpty acc)) then Some(PGroup(o <> "(", List.rev acc), j + 1) else
                match pat j with
                | Some(x, k) when p.Tok k = SYM "," -> items (k + 1) (x :: acc)
                | Some(x, k) when p.Tok k = SYM close -> items k (x :: acc)
                | _ -> None
            items (j + 1) []
        | _ -> None
    let rec lit x = match x with PLit _ -> true | PName _ -> false | PGroup(arr, xs) -> arr || List.exists lit xs
    let rec pats j acc =
        match pat j with
        | Some(x, k) -> pats k (x :: acc)
        | None -> List.rev acc, j
    match p.Tok p.Pos with
    | ID f when Parser.assignable f && not (Parser.foreign.ContainsKey f) && not (Parser.builtins.Contains f) ->
        match pats (p.Pos + 1) [] with
        | ps, j when List.exists lit ps && p.Tok j = SYM "=" -> Some(f, ps, j)
        | _ -> None
    | _ -> None

/// The body after `=`: a `{…}` block of statements, or one expression.
let private body (p: Parser.Parser) = if p.Accept "{" then p.Block "}" else [ SLine(p.Line()); SExpr(p.Expr()) ]

/// A base case becomes a hidden function `f'k` of its pattern's names, and a guard `cond ? ret: f'k: names`
/// that f's general definition puts first in its body.
let private clause (p: Parser.Parser) f (pats: Pat list) eq (modifier: string option) =
    let l = p.Line()
    p.Pos <- eq + 1
    let conds, binds = ResizeArray(), ResizeArray()
    let rec walk v x =
        match x with
        | PName "_" -> ()
        | PName n -> binds.Add(n, v)
        | PLit e -> conds.Add(EBin("=", v, e))
        | PGroup(arr, xs) ->
            if arr then conds.Add(EBin("=", EUn("#", v), EInt(int64 xs.Length)))
            xs |> List.iteri (fun i x -> walk (EIndex(v, EInt(int64 i))) x)
    pats |> List.iteri (fun i x -> walk (EVar $"_{i + 1}") x)
    let names = binds |> Seq.map fst |> List.ofSeq
    if List.distinct names <> names then p.ErrAt eq $"a name appears twice in a base case of '{f}'"
    let body = p.WithParams names (fun () -> body p)
    // a later condition indexes into what an earlier one checked the length of: `c1 ? c2 : F`, not `c1 & c2`
    let cond = Seq.reduceBack (fun c rest -> ETern(c, rest, EBool false)) conds
    let id = p.NewId()
    let value, def =
        if names.IsEmpty then EGroup body, SExpr(EInt 0L)
        else
            let h = $"{f}'{id}"
            p.AddFunction h
            ECall(callMark + h, List.ofSeq (Seq.map snd binds)), SAssign([ EVar h ], [ EBlock(id, names |> List.map (fun n -> [ n ]), body) ])
    let guard = SExpr(ETern(cond, EGroup [ SExpr(ECall(callMark + "ret", [ value ])); SExpr(EInt 0L) ], EInt 0L))
    let clauses = clauses p
    let n, gs, m = match clauses.TryGetValue f with | true, c -> c | _ -> pats.Length, [], None
    if n <> pats.Length then p.ErrAt eq $"base cases of '{f}' have different numbers of parameters"
    clauses.[f] <- (n, gs @ [ SLine l; guard ], (if m.IsSome then m else modifier))
    def

/// `f a b = body` is `f = {body}` with a, b naming _1, _2; its base cases' guards go first.
let private fnDef (p: Parser.Parser) f (ps: string list list) eq (modifier: string option) =
    let defAt = p.Pos
    p.Pos <- eq + 1
    p.AddFunction f
    let id = p.NewId()
    let body = p.WithParams (List.concat ps) (fun () -> body p)
    let clauses = clauses p
    // a modifier on a base case (`memo f 0 = 1`) applies to the whole function
    let modifier = match clauses.TryGetValue f with | true, (_, _, Some m) when modifier.IsNone -> Some m | _ -> modifier
    let body =
        match clauses.TryGetValue f with
        | true, (n, gs, _) ->
            clauses.Remove f |> ignore
            if n <> List.length ps then p.ErrAt defAt $"'{f}' has {List.length ps} parameters but its base cases have {n}"
            gs @ body
        | _ -> body
    let fn = EBlock(id, ps, body)
    SAssign([ EVar f ], [ (match modifier with Some m -> ECall(m, [ fn ]) | None -> fn) ])

/// A modifier `m` before a definition (`memo f a = …`, `lazy f = {…}`, `memo f 0 = 1`): Some m, past it.
let private modifierAhead (p: Parser.Parser) =
    match p.Peek() with
    | ID m when Parser.Parser.Modifiers.Contains m && p.Pos + 2 < p.TokenCount && (match p.Tok(p.Pos + 1) with ID _ -> true | _ -> false) ->
        p.Pos <- p.Pos + 1
        if (fnDefAhead p).IsSome || (clauseAhead p).IsSome || p.Tok(p.Pos + 1) = SYM "=" then Some m else (p.Pos <- p.Pos - 1; None)
    | _ -> None

let private stmtForm (p: Parser.Parser) : Stmt option =
    let modAt = p.Pos
    let modifier = modifierAhead p
    match clauseAhead p with
    | Some(f, pats, eq) -> Some(clause p f pats eq modifier)
    | None ->
    match fnDefAhead p, modifier with
    | Some(f, ps, eq), _ -> Some(fnDef p f ps eq modifier)
    | None, Some m ->
        // `m f = {…}`
        match p.Stmt() with
        | SAssign([ EVar f ], [ EBlock _ as fn ]) -> Some(SAssign([ EVar f ], [ ECall(m, [ fn ]) ]))
        | _ -> p.ErrAt modAt $"{m} marks a function: {m} f x = … or {m} f = {{…}}"
    | None, None -> None

/// Forward references: `f a b = …` (also after a modifier) names a function from the start of its file.
let private declares (p: Parser.Parser) =
    modifierAhead p |> ignore
    match fnDefAhead p with
    | Some(f, _, _) -> Some f
    | None when p.FnAssignAhead() -> (match p.Peek() with ID f -> Some f | _ -> None)
    | None -> None

let private ends (p: Parser.Parser) =
    for KeyValue(f, _) in clauses p do fail 0 $"base case of '{f}' without a general definition `{f} a b = …` after it"

// ---------- code generation ----------

let mutable private byName: IDictionary<string, Inst> = dict []
let private wrapped = HashSet<string>()

/// A user call: user functions own their arguments, except borrowed parameters: those are lent and dropped after.
let private call (g: Codegen.Api) (e: E) =
    match e.k with
    | UserCall(f, args) ->
        let inst = byName.[f]
        let evs =
            List.zip args inst.parms |> List.mapi (fun j (a, (p, _)) ->
                if inst.borrowed.Contains p && Repr.isPtr a.t then
                    match a.k with
                    | Var u when g.lendable u && List.forall Codegen.noAssign (List.skip (j + 1) args) -> g.exB a
                    | _ -> g.ex a, true
                else g.ex a, false)
        let ps = List.zip evs (inst.parms |> List.map snd) |> List.map (fun ((v, _), t) -> Repr.llty t + " " + v)
        let cs = inst.caps |> List.map (fun u -> "ptr " + g.ptrOf u)
        let argList = ps @ cs |> String.concat ", "
        let r = g.instr $"call {Repr.llty inst.ret} @\"{inst.name}\"({argList})"
        List.zip evs args |> List.iter (fun (v, a) -> g.drop v a.t)
        Some r
    | _ -> None

/// `void w.<inst>(ptr env, ptr x, ptr out)`: a C-callable entry for a block instance; env holds its capture
/// pointers, x points at the (borrowed) argument's storage, the owned result is stored to out.
let private wrapper (g: Codegen.Api) (inst: Inst) : string =
    if wrapped.Add inst.name then
        let body =
            g.inFunction Set.empty (fun () ->
                let caps = inst.caps |> List.mapi (fun i _ -> let slot = g.instr $"getelementptr ptr, ptr %%env, i64 {i}" in g.instr $"load ptr, ptr {slot}")
                // one parameter: x points at it; several: x is an array of pointers to them
                let args =
                    inst.parms |> List.mapi (fun i (u, pt) ->
                        let p =
                            if inst.parms.Length = 1 then "%x"
                            else let slot = g.instr $"getelementptr ptr, ptr %%x, i64 {i}" in g.instr $"load ptr, ptr {slot}"
                        let a = g.loadFrom p pt
                        if not (inst.borrowed.Contains u) then g.retain a pt // the instance owns its parameter; C passes a borrowed element
                        Repr.llty pt + " " + a)
                let argList = String.concat ", " (args @ (caps |> List.map (fun p -> "ptr " + p)))
                let r = g.instr $"call {Repr.llty inst.ret} @\"{inst.name}\"({argList})"
                let sr = if resolve inst.ret = TBool then g.instr $"zext i1 {r} to i8" else r
                g.emit $"store {Repr.storage inst.ret} {sr}, ptr %%out"
                g.emit "ret void")
        g.defineLate $"define void @\"w.{inst.name}\"(ptr %%env, ptr %%x, ptr %%out) {{\nentry:\n{body}}}\n\n"
    $"@\"w.{inst.name}\""

/// The env of a callback: an array of the instance's capture pointers (null without captures).
let private envOf (g: Codegen.Api) (inst: Inst) : string =
    if inst.caps.IsEmpty then "null"
    else
        let env = g.alloca $"[{inst.caps.Length} x ptr]"
        inst.caps |> List.iteri (fun i u ->
            let slot = g.instr $"getelementptr ptr, ptr {env}, i64 {i}"
            g.emit $"store ptr {g.ptrOf u}, ptr {slot}")
        env

/// Every instance's definition; parameters arrive as `%p.<u>`, captures as pointers `%c.<u>`.
let private defineAll (g: Codegen.Api) (prog: Program) =
    byName <- prog.insts |> List.map (fun i -> i.name, i) |> dict
    wrapped.Clear()
    for inst in prog.insts do
        let body =
            g.inFunction (set inst.caps) (fun () ->
                for (u, t) in inst.parms do g.emit $"store {Repr.llty t} %%\"p.{u}\", ptr {g.ptrOf u}"
                g.stmts inst.body
                let r = g.ex inst.result
                for u, p in g.locals () do
                    let t = g.varTy u
                    if Repr.isPtr t && not (inst.borrowed.Contains u) then g.release (g.instr $"load ptr, ptr {p}") t
                g.emit $"ret {Repr.llty inst.ret} {r}")
        let ps = (inst.parms |> List.map (fun (u, t) -> $"{Repr.llty t} %%\"p.{u}\"")) @ (inst.caps |> List.map (fun u -> $"ptr %%\"c.{u}\""))
        let parmList = String.concat ", " ps
        g.define $"define {Repr.llty inst.ret} @\"{inst.name}\"({parmList}) {{\nentry:\n{body}}}\n\n"

let feature =
    { Feature.empty with
        name = "functions"
        intercept = [ intercept ]
        llvmNode = [ call ]
        reset = [ fun () -> fnDefs.Clear(); spreads.Clear(); insts.Clear(); instList.Clear(); fixers.Clear() ]
        init =
          [ fun () ->
                Typer.blockCall <- blockCall
                Typer.instances <- (fun () -> (propagateCaptures (); List.ofSeq instList))
                Codegen.blockArg <- (fun g b -> let inst = byName.[b] in [ "ptr", wrapper g inst; "ptr", envOf g inst ])
                Codegen.programs.Add defineAll
                Parser.Parser.StmtForms.Add stmtForm
                Parser.Parser.Declares.Add declares
                Parser.Parser.Ends.Add ends ] }
