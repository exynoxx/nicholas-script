open FSharp.Text
open NS2
open NS2.CodeGen
open NS2.Interpreter
open NS2.PostLLVMGen
open NS2.SSA
open NS2.TypeChecker

let parse (input:string) =
    let cleaned = input.Trim()
    let lexbuf = Lexing.LexBuffer<char>.FromString cleaned
    try
        Parser.main Lexer.tokenize lexbuf
    with ex ->
        Printf.eprintf "(%d,%d): error: %s\n" lexbuf.StartPos.pos_lnum (lexbuf.StartPos.pos_cnum -  lexbuf.StartPos.pos_bol) (match ex with Failure s -> s | _ -> ex.ToString())
        failwith "Parsing error"
  

[<EntryPoint>]
let main argv =
    //run_tests()
    
    let preinput = ""//"# = std.size;$ = io.stdin.line;" //% = str.split;
    //let input = preinput + "b={$1}; x=1001; while (x != 1) { if (x%2==0) {x=x/2} else {x=x*3+1} };";
    let code =
        """
                b = 0;
                res = "";
                if(b>=0) {
                    res = "high";
                } else
                    res = 0;
                    
                print: res;
            """
    
    let input = preinput + code.Trim();
    try
        let raw = parse input
        printfn $"Result: %A{raw}"
        
        let ast = typecheck raw 
        printfn $"typechecked: %A{ast}"
        
        let ssa = ssa_transform ast
        printfn $"SSA: %A{ssa}"
        
        //eval ast
        let llvm = codegen ssa
        printfn $"LLVM: \n############## \n%s{llvm}"
        
        call_llvm llvm
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
