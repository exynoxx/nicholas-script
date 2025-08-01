module NS2.PostLLVMGen

open System.Diagnostics
open System.IO
open System.Reflection

(*
let extractResource (resourceName: string) (outputPath: string) =
    let assembly = Assembly.GetExecutingAssembly()
    for name in Assembly.GetExecutingAssembly().GetManifestResourceNames() do
        printfn "Resource: %s" name
    use stream = assembly.GetManifestResourceStream(resourceName)
    if isNull stream then
        failwithf "Resource '%s' not found" resourceName
    use fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write)
    stream.CopyTo(fileStream)

let runWSLCommand (command: string) =
    let psi = ProcessStartInfo()
    psi.FileName <- "wsl.exe"
    psi.Arguments <- command
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true

    use proc = new Process()
    proc.StartInfo <- psi
    proc.Start() |> ignore

    proc.WaitForExit()

    let output = proc.StandardOutput.ReadToEnd()
    let error = proc.StandardError.ReadToEnd()
    
    if String.length error > 0 then
        printfn "Error:\n%s" error
        
    output

let toWslPath (windowsPath: string) = runWSLCommand $"wslpath -w '%s{windowsPath}'"

let call_llvm(llvm:string) (input_path:string)=
    let programPath = Path.Combine(Path.GetTempPath(), "program.ll")
    File.WriteAllText(programPath, llvm)
    
    let stdlibPath = Path.Combine(Path.GetTempPath(), "stdlib.o")
    extractResource "NS2.StdLib.stdlib.o" stdlibPath
    
    let wslProgramPath = toWslPath programPath
    let wslOutputPath = toWslPath (Path.Combine(Path.GetTempPath(), "program.o"))
    let wslStdlibPath = toWslPath stdlibPath
    let wslFinalPath = toWslPath (Path.Combine(Path.GetTempPath(), "a.out"))
    
    runWSLCommand $"clang -c {wslProgramPath} -o {wslOutputPath}"
    runWSLCommand $"clang {wslOutputPath} {wslStdlibPath} -lm -o {wslFinalPath}"

    printfn $"Created binary in {wslFinalPath}"
    *)

let print_llvm(llvm:string) =
    let text = StdLib.LLVM_declares+"\n"+StdLib.llvm_std_functions+llvm
    printfn $"{text}"    
let write_llvm(llvm:string) =
    let text = StdLib.LLVM_declares+"\n"+StdLib.llvm_std_functions+llvm
    File.WriteAllText("program.ll", text)