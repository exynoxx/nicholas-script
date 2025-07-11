﻿module NS2.PostLLVMGen

open System.Diagnostics
open System.IO

let call_llvm(llvm:string) =
    File.WriteAllText("program.ll", llvm)

    let psi = new ProcessStartInfo("clang.exe", "-S program.ll -o program.s")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    use proc = Process.Start(psi)
    printfn "Clang: %s" (proc.StandardOutput.ReadToEnd())
    proc.WaitForExit()

    let asm = File.ReadAllText("program.s")
    printfn "Generated assembly:\n\n%s" asm