/// String interpolation: `"a{x}b"` is `"a" + x + "b"` (each part converted to str by `+`). The lexer keeps the
/// code parts of a string (with their positions); this parses them and joins the parts.
module Features.Interp

open Ast
open Core

let private parse (p: Parser.Parser) (at: int) =
    match p.Tok at with
    | STR parts ->
        let exprs =
            parts |> List.map (function
                | Lit s -> EStr s
                | Code(src, l, c) ->
                    let sub = p.Sub(Lexer.lex src l c)
                    let e = sub.Expr()
                    while sub.Peek() = TERM do sub.Advance()
                    if sub.Peek() <> EOF then sub.Err $"expected '}}' to end the interpolation, found {describeTok (sub.Peek())}"
                    e)
        EExt("interp", exprs, [])
    | _ -> p.ErrAt at "expected a string"

let private desugar (d: Desugar.Api) env (xs: Expr list) (_: Stmt list list) =
    let parts = List.map (d.expr env) xs
    let first = match parts.Head.k with Str _ -> parts.Head | _ -> mk (Bin("+", mk (Str ""), parts.Head))
    parts.Tail |> List.fold (fun acc p -> mk (Bin("+", acc, p))) first

let feature =
    { Feature.empty with
        name = "interpolation"
        prefix = [ "STR{", parse ]
        desugar = [ "interp", desugar ] }
