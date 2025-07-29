open System.IO
open FSharp.Text
open NS2
open NS2.CodeGen
open NS2.Interpreter
open NS2.PostLLVMGen
open NS2.Print
open NS2.SSA
open NS2.TypeChecker

let StandardLibDeclare =
    """
        declare void @_ns_print_int(i32)
        declare void @_ns_print_string(i8*)
        declare i8* @_ns_int_to_string(i32)
        declare i8* @_ns_string_concat(i8*, i32)
        declare i32 @_ns_pow_int(i32, i32)
    """.Replace("  ", "")


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
    let code =
        match argv.Length with
        | 0 -> failwith "Usage: NS <file>"
        | 1 ->
            File.ReadAllText(argv[0]).Trim()
            
    let input = code;
    try
        let raw = parse input
        //printfn "Parse: %s" (printAst 4 raw)
        
        let ast = typecheck raw
        //printfn "Typechecked: %s" (printAst 4 ast)

        let ssa = ssa_transform ast
        //printfn "SSA: %s" (printAst 4 ssa)
        
        //eval ast
        let llvm = codegen ssa
        //printfn $"LLVM: \n############## \n%s{StandardLibDeclare}\n%s{llvm}"
        printfn $"%s{StandardLibDeclare}\n%s{llvm}"
        
        //call_llvm (StandardLibDeclare+llvm) argv[0]
        write_llvm (StandardLibDeclare+llvm)
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
