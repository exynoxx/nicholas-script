open FSharp.Text
open NS2.Interpreter
open NS2.SSA
open NS2.TypeChecker

let parse (input:string) =
  let lexbuf = Lexing.LexBuffer<char>.FromString input
  Parser.main Lexer.tokenize lexbuf

[<EntryPoint>]
let main argv =
    //run_tests()
    
    let preinput = "# = std.size;$ = io.stdin.line;" //% = str.split;
    let input = preinput + "a=-5; b = if (a < 0) 0 else a;b;";
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
