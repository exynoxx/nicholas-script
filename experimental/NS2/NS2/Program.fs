open System.Text.RegularExpressions
open FSharp.Text
open NS2.Ast
open NS2.Interpreter
open NS2.SSA
open NS2.TypeChecker

let parse (input:string) =
    let cleaned = Regex.Replace(input.ReplaceLineEndings "", @"[ \t]+", " ")
    let lexbuf = Lexing.LexBuffer<char>.FromString cleaned
    try
        Parser.main Lexer.tokenize lexbuf
    with ex ->
        Printf.eprintf "(%d,%d): error: %s\n" lexbuf.StartPos.pos_lnum (lexbuf.StartPos.pos_cnum -  lexbuf.StartPos.pos_bol) (match ex with Failure s -> s | _ -> ex.ToString())
        failwith "Parsing error"
  

[<EntryPoint>]
let main argv =
    //run_tests()
    
    let preinput = "# = std.size;$ = io.stdin.line;" //% = str.split;
    let input = preinput + "b={$1}; x=1001; while (x != 1) { if (x%2==0) {x=x/2} else {x=x*3+1} };";
    //let code =" x=5; while (x != 1){ if (x%2==0) {x=x/2} else {x=x*3+1};print: x; } "
    
   // let input = preinput + code;
    try
        let raw = parse input
        printfn $"Result: %A{raw}"
        let ssa = ssa_transform raw
        printfn $"SSA: %A{ssa}"
        let ast = typecheck ssa 
        printfn $"typechecked: %A{ast}"
        eval ast
    
    with ex ->
        printfn $"ERROR: %s{ex.Message}"
    0
    (*printfn ""
    while true do
        printf "Enter expression: "
        try
            stdin.ReadLine() |> parse |> typecheck |> eval
        with ex ->
            printfn "Parse error: %s" ex.Message
    0*)
