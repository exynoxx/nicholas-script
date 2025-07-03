open FSharp.Text
open NS2.Interpreter
open NS2.TypeChecker

let parse (input:string) =
  let lexbuf = Lexing.LexBuffer<char>.FromString input
  Parser.main Lexer.tokenize lexbuf

[<EntryPoint>]
let main argv =
    //run_tests()
    
    let input = "a=0; b += 1; c ++=1; d=a@1";
    try
        let raw = parse input
        printfn $"Result: %A{raw}"
        let ast = typecheck raw
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
