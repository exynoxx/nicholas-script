/// The standard library lives in runtime.c. Each function there is declared by a signature comment
///   //@ name (param, ...) result [= c_symbol]
/// with types int float str bool, type variables (single capitals), [t] arrays, {k:v} maps, %[t] sets, Q[t] and PQ[t]
/// queues, G[t] graphs, (a,b) tuples and fn(t)->r blocks.
/// The C function receives one descriptor (const char*) per type variable first, then its arguments;
/// a result type containing a tuple also passes the result's descriptor (after the type variables'), so the
/// runtime never rebuilds a tuple layout itself. Type-variable values travel as 64-bit slots, a block as
/// (callback, env). This module only reads the table.
module Stdlib

open System
open System.IO

type STy =
    | SInt
    | SFloat
    | SStr
    | SBool
    | SVar of string
    | SArr of STy
    | STup of STy list
    | SFn of STy * STy
    | SGraph of STy
    | SMap of STy * STy
    | SSet of STy
    | SQ of STy
    | SPQ of STy

let rec showSTy (t: STy) =
    match t with
    | SInt -> "int"
    | SFloat -> "float"
    | SStr -> "str"
    | SBool -> "bool"
    | SVar v -> v
    | SArr t -> "[" + showSTy t + "]"
    | STup ts -> "(" + (ts |> List.map showSTy |> String.concat ",") + ")"
    | SFn(a, b) -> "fn(" + showSTy a + ")->" + showSTy b
    | SGraph t -> "G[" + showSTy t + "]"
    | SMap(k, v) -> "{" + showSTy k + ":" + showSTy v + "}"
    | SSet t -> "%[" + showSTy t + "]"
    | SQ t -> "Q[" + showSTy t + "]"
    | SPQ t -> "PQ[" + showSTy t + "]"

type Sig = { name: string; parms: STy list; ret: STy; csym: string; tvars: string list; rdesc: bool }

let rec private parseTy (s: string) : STy =
    let s = s.Trim()
    match s with
    | "int" -> SInt
    | "float" -> SFloat
    | "str" -> SStr
    | "bool" -> SBool
    | _ when s.StartsWith "fn(" ->
        let close = s.IndexOf ")->"
        SFn(parseTy (s.Substring(3, close - 3)), parseTy (s.Substring(close + 3)))
    | _ when s.StartsWith "G[" -> SGraph(parseTy (s.Substring(2, s.Length - 3)))
    | _ when s.StartsWith "%[" -> SSet(parseTy (s.Substring(2, s.Length - 3)))
    | _ when s.StartsWith "Q[" -> SQ(parseTy (s.Substring(2, s.Length - 3)))
    | _ when s.StartsWith "PQ[" -> SPQ(parseTy (s.Substring(3, s.Length - 4)))
    | _ when s.StartsWith "{" ->
        match splitTop ':' (s.Substring(1, s.Length - 2)) with
        | [ k; v ] -> SMap(parseTy k, parseTy v)
        | _ -> failwith $"stdlib signature: bad map type '{s}'"
    | _ when s.StartsWith "[" -> SArr(parseTy (s.Substring(1, s.Length - 2)))
    | _ when s.StartsWith "(" -> STup(splitTop ',' (s.Substring(1, s.Length - 2)) |> List.map parseTy)
    | _ when s.Length = 1 && Char.IsUpper s.[0] -> SVar s
    | _ -> failwith $"stdlib signature: bad type '{s}'"

/// Splits on `sep` where it is not nested in brackets.
and private splitTop (sep: char) (s: string) : string list =
    if s.Trim() = "" then []
    else
        let parts = ResizeArray<string>()
        let mutable depth, start = 0, 0
        for i in 0 .. s.Length - 1 do
            match s.[i] with
            | '(' | '[' | '{' -> depth <- depth + 1
            | ')' | ']' | '}' -> depth <- depth - 1
            | c when c = sep && depth = 0 -> (parts.Add(s.Substring(start, i - start)); start <- i + 1)
            | _ -> ()
        parts.Add(s.Substring start)
        List.ofSeq parts

let rec private vars (t: STy) =
    match t with
    | SVar v -> [ v ]
    | SArr t | SGraph t | SSet t | SQ t | SPQ t -> vars t
    | SMap(k, v) -> vars k @ vars v
    | STup ts -> List.collect vars ts
    | SFn(a, b) -> vars a @ vars b
    | _ -> []

let rec private hasTup (t: STy) =
    match t with
    | STup _ -> true
    | SArr t | SGraph t | SSet t | SQ t | SPQ t -> hasTup t
    | SMap(k, v) -> hasTup k || hasTup v
    | _ -> false

/// `//@ name (params) result [= csym]`
let private parseSig (line: string) : Sig =
    let body = line.Substring(3).Trim()
    let body, csym =
        match body.LastIndexOf " = " with
        | -1 -> body, None
        | i -> body.Substring(0, i), Some(body.Substring(i + 3).Trim())
    let name = body.Substring(0, body.IndexOf ' ')
    let open' = body.IndexOf '('
    // the parameter list ends at the ')' that closes the first '('
    let mutable depth, close = 0, -1
    for i in open' .. body.Length - 1 do
        if close < 0 then
            match body.[i] with
            | '(' -> depth <- depth + 1
            | ')' ->
                depth <- depth - 1
                if depth = 0 then close <- i
            | _ -> ()
    let parms = splitTop ',' (body.Substring(open' + 1, close - open' - 1)) |> List.map parseTy
    let ret = parseTy (body.Substring(close + 1))
    { name = name; parms = parms; ret = ret; csym = defaultArg csym ("ns_" + name)
      tvars = (parms @ [ ret ]) |> List.collect vars |> List.distinct
      rdesc = hasTup ret }

let sigs : Sig list =
    use s = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream "runtime.c"
    (new StreamReader(s)).ReadToEnd().Split('\n')
    |> Array.filter (fun l -> l.StartsWith "//@ ")
    |> Array.map parseSig
    |> List.ofArray

let names = sigs |> List.map (fun s -> s.name) |> set
let zeroArity = sigs |> List.filter (fun s -> s.parms.IsEmpty) |> List.map (fun s -> s.name) |> set
let overloads name = sigs |> List.filter (fun s -> s.name = name)
