/// What sets and queues share: their literal syntax `K[a, b]` (K a word, or `%` for a set), the constructors
/// `set:xs` / `Q:xs` / `PQ:xs` (also of a tuple's values), adding elements with `c + x` and `c ++ xs`, emptying
/// with `c ** 0`, and the runtime's generic `add`/`del` (by descriptor). Set.fs and Queue.fs register their kinds
/// with `collection`.
module Features.Coll

open System
open Ast
open Core
open Typer

/// Collection kinds (TCon names) built by `K:xs` and grown with `+` / `++`.
let kinds = System.Collections.Generic.HashSet<string>()
let private isColl t = match resolve t with TCon(n, [ _ ]) -> kinds.Contains n | _ -> false

/// A lexer rule for `word[` (or `%[`) not after an operand: a collection literal's opening bracket.
let literalOpener (word: string) (ctx: Lexer.Ctx) =
    let i = ctx.pos ()
    if ctx.src.Length > i + word.Length && String.CompareOrdinal(ctx.src, i, word + "[", 0, word.Length + 1) = 0 && not (ctx.endsExpr ()) then
        ctx.sym (word + "[")
        ctx.moveTo (i + word.Length + 1)
        true
    else false

/// `K[a, b]` is `K:[a, b]`.
let literal (kind: string) (p: Parser.Parser) (_: int) =
    let items = if p.IsSym "]" then [] else p.ExprList()
    p.Expect "]"
    ECall(kind, [ EArr items ])

let private ctor (name: string) (args: E list) =
    match args with
    | [ { t = t } as x ] when (match resolve t with TTup _ -> true | _ -> false) ->
        // `set:(a,b,c)`: the tuple's values (converted to the first one's type) as an array
        let (TTup ts) = resolve t
        let tv = typed (Var(declare cur (Desugar.fresh "t") t)) t
        let (Var u) = tv.k
        let vs = ts |> List.mapi (fun k ty -> conv (typed (Call("tget", [ tv; typed (Int(int64 k)) TInt ])) ty) ts.Head)
        Some(builtin name [ typed (Seq([ Assign(u, x) ], typed (Arr vs) (TArr ts.Head))) (TArr ts.Head) ])
    | [ xs ] ->
        let el = elemOf name xs
        Some(typed (Call(name, [ (match resolve xs.t with TArr _ -> xs | _ -> stdCall "arr" [ xs ]) ])) (TCon(name, [ el ])))
    | _ -> None

let private bins op (_: bool) (a: E) (b: E) =
    let L = resolve a.t
    match op, L with
    // `s ++ xs`: add every element of a collection (array, set, Q, PQ); `s ++ x` adds one element
    | "++", TCon(_, [ el ]) when isColl L ->
        // element type still unknown (`s = %[]`): a collection operand adds its elements, it is not one element
        let unknown = match resolve el with THole _ -> true | _ -> false
        match resolve b.t with
        | TArr e | TCon(_, [ e ]) when (resolve b.t |> function TArr _ -> true | t -> isColl t) && (unknown || not (unify b.t el)) && unify e el ->
            let av, x = declare cur (Desugar.fresh "c") L, Desugar.fresh "_"
            let cv = typed (Var av) L
            let b = match resolve b.t with TCon(n, _) when Lower.drains.ContainsKey n -> stdCall "arr" [ b ] | _ -> b // iterating a queue would drain it
            let it = { kind = IEach; src = b; el = x; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do(mk (Bin("+", cv, mk (Var x)))) ]; post = [] }
            Some(typed (Seq([ Assign(av, a); Do(typeE (mk (Iter it))) ], cv)) L)
        | _ -> Some(typed (Call("add", [ a; own (conv b el) ])) L)
    | "+", TCon(_, [ el ]) when isColl L -> Some(typed (Call("add", [ a; own (conv b el) ])) L)
    | "**", _ when isColl L -> Some(stdCall "keep" [ a; conv b TInt ]) // `st**F`: empty in place
    | _ -> None

/// Registers a collection kind built by `name:xs` (its codegen constructor is the runtime function `ctorC`).
let collection (name: string) (ctorC: string) =
    { Feature.empty with
        name = name
        builtins = [ name ]
        init = [ fun () -> kinds.Add name |> ignore; Lower.collects.Add name |> ignore ]
        prims = [ name, ctor name ]
        llvm =
          [ name, fun g e _ vs ->
                let coll = g.rt "ptr" ctorC [ g.desc e.t ]
                g.rt "void" "ns_add_all" [ g.desc e.t; "ptr", coll; "ptr", vs.[0] ] |> ignore
                coll ] }

let feature =
    { Feature.empty with
        name = "collections"
        bin = [ bins ]
        effects = [ "add", Feature.Mutating; "del", Feature.Mutating ]
        kept = [ "add", 1; "del", 1 ]
        llvm =
          [ "add", fun g _ args vs -> (g.rt "void" "ns_add" [ g.desc args.Head.t; "ptr", vs.[0]; "ptr", g.spill vs.[1] args.[1].t ] |> ignore; vs.[0])
            "del", fun g _ args vs -> (g.rt "void" "ns_del" [ g.desc args.Head.t; "ptr", vs.[0]; "ptr", g.spill vs.[1] args.[1].t ] |> ignore; vs.[0]) ] }
