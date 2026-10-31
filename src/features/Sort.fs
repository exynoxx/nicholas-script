/// `x${key}`: x sorted by key (stable; a map sorts its (k, v) pairs). Pure syntax: a map to (key, index, element)
/// triples, the runtime's `sort`, and a map back to the elements.
module Features.Sort

open Ast
open Core

let private lex (ctx: Lexer.Ctx) =
    if ctx.at 0 = '$' && ctx.at 1 = '{' && ctx.endsExpr () then
        ctx.sym "${"
        ctx.moveTo (ctx.pos () + 2)
        true
    else false

let private desugar (d: Desugar.Api) env (xs: Expr list) (blocks: Stmt list list) =
    let src, body = xs.Head, blocks.Head
    // x{(key, @, _)}.sort{_3}: the index breaks ties, so equal keys keep their order; a map sorts its (k,v) pairs
    let keyed =
        d.iter (d.ownsI body env) IMap (mk (Call("entries", [ d.expr env src ]))) (fun env' ->
            let ss = d.block env' body
            let last = ss |> List.findIndexBack (function Do _ -> true | _ -> false)
            ss |> List.mapi (fun j s ->
                match s with
                | Do key when j = last -> Do(mk (Call("tuple", [ key; mk (Var env'.["@"]); mk (Var env'.["_"]) ])))
                | s -> s))
    d.iter env IMap (mk (Call("sort", [ keyed ]))) (fun env' -> [ Do(mk (Index(mk (Var env'.["_"]), mk (Int 2L)))) ])

let feature =
    { Feature.empty with
        name = "sort"
        lex = [ lex ]
        openers = [ "${" ]
        postfix = [ "${", fun p e -> EExt("sort", [ e ], [ p.SectionBlock "}" ]) ]
        desugar = [ "sort", desugar ] }
