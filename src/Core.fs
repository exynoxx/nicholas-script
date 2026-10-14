/// The small core language shared by desugar, typer and codegen, plus its printer.
module Core

open System.Text

type Ty =
    | TInt
    | TFloat
    | TStr
    | TBool
    | TArr of Ty
    | TMap of Ty * Ty
    | TSet of Ty
    | TQ of Ty
    | TPQ of Ty
    | TGraph of Ty              // nodes of this type, int weights
    | TTup of Ty list
    | TFn of int
    | THole of Ty option ref
    | TUnk

let rec resolve t =
    match t with
    | THole r ->
        match r.Value with
        | Some t -> resolve t
        | None -> t
    | _ -> t

type E = { k: K; t: Ty }

and K =
    | Int of int64
    | Float of float
    | Str of string
    | Bool of bool
    | Var of string
    | Arr of E list
    | Bin of string * E * E
    | If of E * E * E
    | Call of string * E list // builtins/primitives; "@name" = function instance
    | Index of E * E
    | Iter of Iter            // removed by the typer (via Lower)
    | Fn of int * S list      // block literal: id, body (params are _1.._n)
    | Conv of E               // convert to this node's t
    | Seq of S list * E

and Iter = { kind: string; src: E; el: string; ix: string; acc: string; body: S list }

and S =
    | Assign of string * E
    | SetIdx of E * E * E
    | While of E * S list
    | Do of E
    | Line of int

let mk k = { k = k; t = TUnk }

let rec showTy t =
    match resolve t with
    | TInt -> "int"
    | TFloat -> "float"
    | TStr -> "str"
    | TBool -> "bool"
    | TArr e -> "[" + showTy e + "]"
    | TMap(k, v) -> "{" + showTy k + ":" + showTy v + "}"
    | TSet e -> "%" + showTy e
    | TQ e -> "Q " + showTy e
    | TPQ e -> "PQ " + showTy e
    | TGraph e -> "G " + showTy e
    | TTup ts -> "(" + (ts |> List.map showTy |> String.concat ",") + ")"
    | TFn id -> $"fn{id}"
    | THole _ -> "?"
    | TUnk -> ""

/// Prints core as NS-like text; typed nodes get a `:type` suffix on variables, calls and conversions.
let rec showE (e: E) : string =
    let ann s = if e.t = TUnk then s else s + ":" + showTy e.t
    match e.k with
    | Int v -> string v
    | Float v -> $"{v}"
    | Str s -> Ast.quote s
    | Bool b -> if b then "T" else "F"
    | Var n -> ann n
    | Arr xs -> "[" + (xs |> List.map showE |> String.concat ",") + "]"
    | Bin(op, a, b) -> "(" + showE a + op + showE b + ")"
    | If(c, a, b) -> "(" + showE c + " ? " + showE a + " : " + showE b + ")"
    | Call(f, args) -> ann (f + "(" + (args |> List.map showE |> String.concat ", ") + ")")
    | Index(x, i) -> showE x + "@" + showE i
    | Iter it -> $"{it.kind}({showE it.src}){{{it.el} {it.ix} {it.acc}| {showBody it.body}}}"
    | Fn(id, body) -> $"fn{id}{{{showBody body}}}"
    | Conv x -> ann ("conv(" + showE x + ")")
    | Seq(ss, x) -> "(" + showBody ss + "; " + showE x + ")"

and showBody ss = ss |> List.choose (function Line _ -> None | s -> Some(showS 0 s)) |> String.concat "; "

and showS ind s =
    let pad = String.replicate ind "  "
    match s with
    | Assign(n, e) -> pad + n + " = " + showE e
    | SetIdx(x, i, v) -> pad + showE x + "@" + showE i + " = " + showE v
    | Do e -> pad + showE e
    | Line n -> pad + $"# line {n}"
    | While(c, body) ->
        let sb = StringBuilder()
        sb.Append(pad + showE c + " ~{\n") |> ignore
        for s in body do
            match s with
            | Line _ -> ()
            | s -> sb.Append(showS (ind + 1) s + "\n") |> ignore
        sb.Append(pad + "}").ToString()

let showStmts (ss: S list) =
    ss |> List.choose (function Line _ -> None | s -> Some(showS 0 s)) |> String.concat "\n"
