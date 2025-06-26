open System
open FSharp.Text
open NS2
open NS2.Ast
open NS2.Interpreter
open NS2.TypeChecker

let evaluate (input:string) =
  let lexbuf = Lexing.LexBuffer<char>.FromString input
  Parser.main Lexer.tokenize lexbuf
  
let rec equal a b =
    match a, b with
    | Int x, Int y -> x = y
    | Id x, Id y -> x = y
    | Binop (l1, op1, r1), Binop (l2, op2, r2) ->
        op1 = op2 && equal l1 l2 && equal r1 r2
    | Array xs, Array ys ->
        List.length xs = List.length ys && List.forall2 equal xs ys
    | Index (a,b), Index (x,y) -> a=x && b=y
    | Func xs, Func ys ->
        List.length xs = List.length ys && List.forall2 equal xs ys
    | _ -> failwith "%A %A" a b 

let assert_same (input:string, expected:AST) =
    let actual = evaluate input |> List.head
    if not (equal actual expected) then
        failwithf "AST test failed: expected %A but got %A" expected actual
    else
        printfn "passed"

let test_arithmetic () =
    let input = "1+2*3/4"
    let expected =Binop(Int 1, "+", Binop(Binop(Int 2, "*", Int 3), "/", Int 4))

    assert_same (input, expected)
let test_array_expression1 () =
    let input = "[]"
    let expected = Array []
    assert_same (input, expected)
    
let test_array_expression2 () =
    let input = "[1000]"
    let expected = Array [Int 1000]
    assert_same (input, expected)
    
let test_array_expression3 () =
    let input = "[1,2+3,4]"
    let expected = Array [Int 1; Binop (Int 2, "+", Int 3); Int 4]
    assert_same (input, expected)
    
let test_array_index () =
    let input = "a@2*10"
    let expected = Binop(Index (Id "a", Int 2), "*", Int 10)
    assert_same (input, expected)

let test_func () =
    let input = "{10;a}"
    let expected = Func [Int 10; Id "a"]
    assert_same (input, expected)

[<EntryPoint>]
let main argv =
    test_arithmetic ()
    test_array_expression1 () 
    test_array_expression2 () 
    test_array_expression3 () 
    test_array_index ()    
    test_func ()
    
    let input = "\"STR\";1+1"
    try
        let result = evaluate input
        //typecheck result |> ignore
        printfn "Result: %A" result
    with ex ->
        printfn "Parse error: %s" ex.Message
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
