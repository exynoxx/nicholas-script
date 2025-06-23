open System
open FSharp.Text

let evaluate (input:string) =
  let lexbuf = Lexing.LexBuffer<char>.FromString input
  let output = Parser.main Lexer.tokenize lexbuf
  string output
  

[<EntryPoint>]
let main argv =
    printf "Enter arithmetic expression: "
    let input = "3*7+1"
    try
        let result = evaluate input
        printfn "Result: %s" result
    with ex ->
        printfn "Parse error: %s" ex.Message
    0
