module NS2.StdLib

open System
open NS2.Ast

let reverse input =
    input
    |> Seq.rev
    |> Seq.toArray
    |> System.String


let eval_std_function (name:string, args: AST list) =
    match name with
    | str when name.StartsWith("str") ->
        let input = match args with | [String x] -> x | _ -> failwith "Argument not string"
        let result = 
            match str with
            | "str.rev" -> String (reverse input)
            | "str.length" -> String (input.Length.ToString())
            | "str.trim" -> String (input.Trim())
            | x -> failwith $"{x} not implemented yet"
        Some result
    | io when name.StartsWith("io") ->
        let result =
            match io with
            | "io.println" ->
                match args.Head with
                | String s -> printfn $"{s}"
                | Int s -> printfn $"{s}"
                | Array a -> printfn $"%A{a}"
                Nop
            | "io.stdin.line" -> String (stdin.ReadLine())
            | "io.stdin.all" -> String (stdin.ReadToEnd())
            | x -> failwith $"{x} not implemented yet"
        Some result
    | _ -> None