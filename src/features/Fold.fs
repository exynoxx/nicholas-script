/// Folds and scans: `xs/{$ + _}` / `xs\{…}` with a block (`$` the accumulator), and with an operator
/// `xs/+ * > < & | ^ -` / `xs\+`. A fold of nothing is the operator's identity (or a panic). The loops themselves
/// are Fusion's.
module Features.Fold

open System
open Ast
open Core
open Typer

let private ops = [ "+"; "*"; ">"; "<"; "&"; "|"; "^"; "-" ]

/// `/op` is a fold, except `/-` followed by an operand (`10/-x`, `10/-#x`, `10/-.5`):
/// `-` is the only fold operator that is also a prefix operator.
let private opAfter (ctx: Lexer.Ctx) =
    ops |> List.tryFind (fun op ->
        if ctx.at 1 <> op.[0] then false
        elif op <> "-" then true
        else
            let k = ctx.skipWs (ctx.pos () + 2) - ctx.pos ()
            let c = ctx.at k
            not (Char.IsLetterOrDigit c || "_$@\"(-".IndexOf c >= 0
                 || (c = '#' && " \t\r\n\000".IndexOf(ctx.at (k + 1)) < 0)
                 || (c = '.' && Char.IsAsciiDigit(ctx.at (k + 1))) || (c = '%' && ctx.at (k + 1) = '[')))

let private lex (ctx: Lexer.Ctx) =
    let c, i = ctx.at 0, ctx.pos ()
    if (c <> '/' && c <> '\\') || not (ctx.endsExpr ()) then false
    else
        let j = ctx.skipWsNl (i + 1)
        if j < ctx.src.Length && ctx.src.[j] = '{' then
            // `x / {..}` is a fold too: dividing by a block can never type-check. The `{` may be on the next line.
            ctx.sym (string c + "{")
            ctx.moveTo (j + 1)
            true
        else
            match opAfter ctx with
            | Some op -> (ctx.sym (string c + op); ctx.moveTo (i + 2); true)
            | None -> false

let private desugar kind (d: Desugar.Api) env (xs: Expr list) (_: Stmt list list) =
    let op = match xs.[1] with EStr op -> op | _ -> failwith "fold: operator"
    d.iter env kind (d.expr env xs.[0]) (fun env' ->
        let acc, el = mk (Var env'.["$"]), mk (Var env'.["_"])
        let body =
            match op with
            | ">" -> If(mk (Bin(">", acc, el)), acc, el)
            | "<" -> If(mk (Bin("<", acc, el)), acc, el)
            | op -> Bin(op, acc, el)
        [ Do(mk body) ])

/// `foldid(op, acc)`: the identity of a fold op, the value of an empty `xs/op` (Lower.iter)
let private identity (e: E) =
    match e.k with
    | Call("foldid", [ { k = Str op }; acc ]) ->
        let acc = typeE acc
        match op, resolve acc.t with
        | ("+" | "|" | "^"), TInt -> Some(typed (Int 0L) TInt)
        | "*", TInt -> Some(typed (Int 1L) TInt)
        | "&", TInt -> Some(typed (Int -1L) TInt)
        | "+", TFloat -> Some(typed (Float 0.0) TFloat)
        | "*", TFloat -> Some(typed (Float 1.0) TFloat)
        | "+", TStr -> Some(typed (Str "") TStr)
        | "+", TArr _ -> Some(typed (Arr []) acc.t)
        | "&", TBool -> Some(typed (Bool true) TBool)
        | ("|" | "^"), TBool -> Some(typed (Bool false) TBool)
        | _ -> Some(typeE (mk (Seq([ Do(mk (Call("panic", [ mk (Str "fold of empty collection") ]))) ], acc))))
    | _ -> None

let feature =
    { Feature.empty with
        name = "fold"
        lex = [ lex ]
        openers = [ "/{"; "\\{" ]
        closers = [ for op in ops do yield "/" + op; yield "\\" + op ]
        postfix =
          [ yield "/{", (fun p e -> EIter(IFold, e, p.Block "}"))
            yield "\\{", (fun p e -> EIter(IScan, e, p.Block "}"))
            for op in ops do
                yield "/" + op, (fun _ e -> EExt("fold", [ e; EStr op ], []))
                yield "\\" + op, (fun _ e -> EExt("scan", [ e; EStr op ], [])) ]
        desugar = [ "fold", desugar IFold; "scan", desugar IScan ]
        intercept = [ identity ] }
