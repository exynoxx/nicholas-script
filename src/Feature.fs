/// A feature is one language construct or compiler mechanism, with a hook into every stage it touches. The kernel
/// (src/kernel) knows no feature: each stage consults the tables `register` fills. Features are compiled after the
/// kernel and listed in AllFeatures.fs; a feature compiled later may also call an earlier one's functions directly.
module Feature

open Ast
open Core

/// What a primitive does, for the optimiser (Opt's effect tables).
type Effect =
    | Total     // computes a value, never fails
    | Partial   // computes a value, may panic
    | Mutating  // changes its first argument in place

/// A type constructor (`TCon(name, args)`): how it prints and its runtime descriptor (see runtime.c).
type TypeCon = { con: string; show: (Ty -> string) -> Ty list -> string; desc: (Ty -> string) -> Ty list -> string }

type Feature =
    { name: string
      // ---- lexer ----
      /// rules tried at each token start (true: lexed a token)
      lex: (Lexer.Ctx -> bool) list
      /// opening brackets after which a leading operator gets an implicit `_`
      openers: string list
      /// symbol tokens that end an expression
      closers: string list
      // ---- parser ----
      /// names callable as `f:x`
      builtins: string list
      prefix: (string * (Parser.Parser -> int -> Expr)) list
      postfix: (string * (Parser.Parser -> Expr -> Expr)) list
      suffix: (string * (Parser.Parser -> Expr -> Expr)) list
      stmtPrefix: (string * (Parser.Parser -> Stmt)) list
      assignRhs: (string * (Parser.Parser -> Expr)) list
      fnStarts: Tok list
      modifiers: string list
      // ---- desugar ----
      desugar: (string * (Desugar.Api -> Desugar.Env -> Expr list -> Stmt list list -> E)) list
      corePasses: (int * string * (S list -> S list)) list
      perElement: string list
      // ---- typer ----
      types: TypeCon list
      intercept: (E -> E option) list
      prims: (string * (E list -> E option)) list
      bin: (string -> bool -> E -> E -> E option) list
      index: (E -> E -> E option) list
      freshness: (string * (E -> bool)) list
      assignRewrite: (string -> E -> S option) list
      bodyRewrite: (int -> int -> S list -> S list) list
      noSpread: (int -> bool) list
      fixerExempt: (int -> bool) list
      prologue: (unit -> S list) list
      reset: (unit -> unit) list
      // ---- optimiser ----
      effects: (string * Effect) list
      optPasses: Opt.Pass list
      // ---- code generation ----
      llvm: (string * (Codegen.Api -> E -> E list -> string list -> string)) list
      llvmRaw: (string * (Codegen.Api -> E -> E list -> string)) list
      /// pass-through primitives: how many leading arguments their result owns
      kept: (string * int) list
      // ---- anything else: registration into another feature's own tables ----
      init: (unit -> unit) list }

let empty =
    { name = ""; lex = []; openers = []; closers = []
      builtins = []; prefix = []; postfix = []; suffix = []; stmtPrefix = []; assignRhs = []; fnStarts = []; modifiers = []
      desugar = []; corePasses = []; perElement = []
      types = []; intercept = []; prims = []; bin = []; index = []; freshness = []; assignRewrite = []
      bodyRewrite = []; noSpread = []; fixerExempt = []; prologue = []; reset = []
      effects = []; optPasses = []
      llvm = []; llvmRaw = []; kept = []
      init = [] }

/// Fills every stage's tables from f. Two features claiming the same token, construct or primitive is an error
/// (typer prims and rules are lists instead: tried in registration order).
let register (f: Feature) =
    let once (d: System.Collections.Generic.Dictionary<string, 'v>) what (k, v) =
        if d.ContainsKey k then failwith $"feature {f.name}: {what} '{k}' is already taken"
        d.[k] <- v
    Lexer.rules.AddRange f.lex
    for o in f.openers do Lexer.openers.Add o |> ignore
    for c in f.closers do Lexer.closers.Add c |> ignore
    for b in f.builtins do Parser.builtins.Add b |> ignore
    List.iter (once Parser.Parser.PrefixHooks "prefix token") f.prefix
    List.iter (once Parser.Parser.PostfixHooks "postfix token") f.postfix
    List.iter (once Parser.Parser.SuffixHooks "suffix token") f.suffix
    List.iter (once Parser.Parser.StmtHooks "statement token") f.stmtPrefix
    List.iter (once Parser.Parser.AssignHooks "assignment token") f.assignRhs
    for t in f.fnStarts do Parser.Parser.FnStarts.Add t |> ignore
    for m in f.modifiers do Parser.Parser.Modifiers.Add m |> ignore
    List.iter (once Desugar.ext "construct") f.desugar
    Desugar.passes.AddRange f.corePasses
    for c in f.perElement do perElement.Add c |> ignore
    for t in f.types do
        once conShow "type" (t.con, t.show)
        Repr.conDesc.[t.con] <- t.desc
    Typer.intercept.AddRange f.intercept
    for name, h in f.prims do
        match Typer.prims.TryGetValue name with
        | true, hs -> hs.Add h
        | _ -> Typer.prims.[name] <- ResizeArray [ h ]
    Typer.bins.AddRange f.bin
    Typer.indexers.AddRange f.index
    List.iter (once Typer.freshness "freshness of") f.freshness
    Typer.assignRewrites.AddRange f.assignRewrite
    Typer.bodyRewrites.AddRange f.bodyRewrite
    Typer.noSpread.AddRange f.noSpread
    Typer.fixerExempt.AddRange f.fixerExempt
    Typer.prologue.AddRange f.prologue
    Typer.resets.AddRange f.reset
    for name, e in f.effects do
        (match e with Total -> Opt.totalBuiltins | Partial -> Opt.partialBuiltins | Mutating -> Opt.mutatingBuiltins).Add name |> ignore
    Opt.passes.AddRange f.optPasses
    List.iter (once Codegen.calls "primitive") f.llvm
    List.iter (once Codegen.rawCalls "primitive") f.llvmRaw
    for name, k in f.kept do Codegen.kept.[name] <- k
    for i in f.init do i ()
