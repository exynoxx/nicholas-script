/// `?? stmt`: run the statement, and skip it if it fails at runtime (missing key, index out of range, panic):
/// a setjmp handler frame around it.
module Features.Try

open Ast
open Core
open Typer

let feature =
    { Feature.empty with
        name = "try"
        stmtPrefix = [ "??", fun p -> SExpr(EExt("try", [ EGroup [ p.Stmt(); SExpr(EInt 0L) ] ], [])) ]
        desugar =
          [ "try", fun d env xs _ ->
                let call f = mk (Call(f, []))
                mk (If(call "try", mk (Seq([ Do(d.expr env xs.Head) ], call "untry")), mk (Int 0L))) ]
        prims =
          [ "try", (function [] -> Some(typed (Call("try", [])) TBool) | _ -> None)
            "untry", (function [] -> Some(typed (Call("untry", [])) TInt) | _ -> None) ]
        llvm =
          [ "try", fun g _ _ _ ->
                // a handler frame in this function; _setjmp returns again (nonzero) when the statement panics
                let buf = g.alloca "[512 x i8], align 16"
                g.rt "void" "ns_try" [ "ptr", buf ] |> ignore
                // Win64's _setjmp takes a frame to unwind to (null: plain longjmp); other ABIs ignore the extra argument
                g.declare "_setjmp" "declare i32 @_setjmp(ptr, ptr) returns_twice"
                let r = g.instr $"call i32 @_setjmp(ptr {buf}, ptr null) returns_twice"
                g.instr $"icmp eq i32 {r}, 0"
            "untry", fun g _ _ _ -> (g.rt "void" "ns_untry" [] |> ignore; "0") ] }
