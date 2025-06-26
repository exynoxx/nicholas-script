open FSharp.Text
open NS2.Interpreter
open NS2.TypeChecker
open NS2.UnitTests

let parse (input:string) =
  let lexbuf = Lexing.LexBuffer<char>.FromString input
  Parser.main Lexer.tokenize lexbuf

[<EntryPoint>]
let main argv =
    //run_tests()
    
    let input = "f = {1+1};f"
    try
        let raw = parse input
        printfn "Result: %A" raw
        let ast = typecheck raw
        printfn "typechecked: %A" ast
        eval ast
    with ex ->
        printfn "ERROR: %s" ex.Message
    0
    (*printfn ""
    while true do
        printf "Enter arithmetic expression: "
        try
            let input = stdin.ReadLine()
            let result = evaluate input
            let _ = typecheck result |> ignore
            eval result
        with ex ->
            printfn "Parse error: %s" ex.Message
    0*)
