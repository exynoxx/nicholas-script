module NS2.PostLLVMGen

open System.Diagnostics
open System.IO
open System.Threading

let call_llvm(llvm:string) =
    File.WriteAllText("program.ll", llvm)

    let psi = new ProcessStartInfo("clang.exe", "-c -S program.ll")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    use proc = Process.Start(psi)
    proc.WaitForExit()

    printfn "Clang: %s" (proc.StandardOutput.ReadToEnd())
    printfn "Clang err: %s" (proc.StandardError.ReadToEnd())

    let asm = File.ReadAllText("program.s")
    printfn "Generated assembly:\n\n%s" asm