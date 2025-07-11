﻿module NS2.StdLib

open System
open NS2.Ast

let reverse input =
    input
    |> Seq.rev
    |> Seq.toArray
    |> System.String

let lookup_std_function (name:string) =
    match name with
    | "std.size" -> true
    | "str.rev" -> true
    | "str.trim" -> true
    | "print" -> true
    | "io.stdin.line" -> true
    | "io.stdin.all" -> true
    | _ -> false

let eval_std_function (name:string, args: AST list) =
    match name with
    | "std.size" ->
        match args with
        | [String x] -> Some (String (x.Length.ToString()))
        | [Array x] -> Some (String (x.Length.ToString()))
        | _ -> failwith "std.size Argument not string or array"
    | "str.rev" ->
        let input = match args with | [String x] -> x | _ -> failwith "Argument not string"
        Some (String (reverse input))
    | "str.trim" ->
        let input = match args with | [String x] -> x | _ -> failwith "Argument not string"
        Some (String (input.Trim()))
    | "print" ->
        match args.Head with
        | String s -> printfn $"{s}"
        | Int s -> printfn $"{s}"
        | Array a -> printfn $"%A{a}"
        Some Nop
    | "io.stdin.line" -> Some (String (stdin.ReadLine()))
    | "io.stdin.all" -> Some (String (stdin.ReadToEnd()))
    | _ -> None