module Program

open System
open System.IO
open System.Diagnostics
open Ast

let private showTyped (p: Typer.Program) =
    let fns =
        p.insts
        |> List.map (fun i ->
            let ps = i.parms |> List.map (fun (u, t) -> u + ":" + Core.showTy t) |> String.concat ", "
            let caps = if i.caps.IsEmpty then "" else sprintf " captures [%s]" (String.concat ", " i.caps)
            sprintf "fn %s(%s)%s -> %s {\n%s\n  => %s\n}" i.name ps caps (Core.showTy i.ret) (Core.showStmts i.body) (Core.showE i.result))
    String.concat "\n\n" (fns @ [ "main:\n" + Core.showStmts p.main ])

/// Runs the pipeline up to `stage` and returns its printable output.
let private pipeline (stage: string) (path: string) : string =
    let toks = Lexer.lex (File.ReadAllText path) 1
    if stage = "tokens" then toks |> List.map (fun t -> sprintf "%d: %A" t.line t.tok) |> String.concat "\n"
    else
        let ast = Parser.parse toks path
        if stage = "ast" then sprintf "%A" ast
        else
            let core = Desugar.desugar ast
            if stage = "core" then Core.showStmts core
            else
                let typed = Typer.check core
                if stage = "typed" then showTyped typed else Codegen.generate typed

let private run (exe: string) (args: string) (stdin: string option) =
    let psi = ProcessStartInfo(exe, args, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin.IsSome)
    use p = Process.Start psi
    stdin |> Option.iter (fun s -> p.StandardInput.Write s; p.StandardInput.Close())
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, out.Result, err.Result

/// runtime.c, precompiled to LLVM IR when the compiler was built (see NS3.fsproj).
let private runtimeIR =
    lazy
        (use s = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream "runtime.ll"
         (new StreamReader(s)).ReadToEnd())

/// Self-contained module: our IR plus runtime.c compiled to IR, minus our declares of what the runtime provides.
let private pureLL (ll: string) : string =
    let rtIR = runtimeIR.Force()
    let nameOf (line: string) = let a = line.IndexOf '@' in line.Substring(a + 1, line.IndexOf('(', a) - a - 1)
    let provided =
        rtIR.Split('\n')
        |> Array.filter (fun l -> l.StartsWith "define " || l.StartsWith "declare ")
        |> Array.map nameOf
        |> set
    let ours = ll.Split('\n') |> Array.filter (fun l -> not (l.StartsWith "declare " && provided.Contains(nameOf l)))
    let isHeader (l: string) = l.StartsWith "source_filename" || l.StartsWith "target " || l.StartsWith "; ModuleID"
    let rtLines = rtIR.Split('\n')
    String.concat "\n" (Array.filter isHeader rtLines) + "\n\n; ---- NS3 program ----\n" + String.concat "\n" ours
    + "\n; ---- NS3 runtime ----\n" + String.concat "\n" (Array.filter (isHeader >> not) rtLines)

/// LLVM 21+ vectorises filter->fold early exits in a way that is ~10x slower for NS loops; turn it off when clang knows the flag.
let private noEarlyExitVec =
    lazy
        (let tmp = Path.Combine(Path.GetTempPath(), "ns-flagcheck.c")
         File.WriteAllText(tmp, "int x;")
         let code, _, _ = run "clang" (sprintf "-c -mllvm -enable-early-exit-vectorization=false \"%s\" -o /dev/null" tmp) None
         if code = 0 then " -mllvm -enable-early-exit-vectorization=false" else "")

/// Compiles a source file to a native binary at `exe`; returns an error message on failure.
let private build (path: string) (exe: string) : string option =
    try
        let ll = Path.ChangeExtension(exe, ".ll")
        // whole-program module so LLVM can inline the runtime (array access, refcounts, ...)
        File.WriteAllText(ll, pureLL (pipeline "ll" path))
        // NS3_ASAN=1 builds with Address/LeakSanitizer to verify reference counting
        let asan = if Environment.GetEnvironmentVariable "NS3_ASAN" = "1" then "-g -fsanitize=address" else ""
        let asan = asan + noEarlyExitVec.Force()
        let code, _, err = run "clang" (sprintf "-O2 -w %s \"%s\" -lm -o \"%s\"" asan ll exe) None
        if code = 0 then None else Some("clang failed:\n" + err)
    with
    | CompileError(line, msg) -> Some(sprintf "%s:%d: error: %s" path line msg)
    | ex -> Some(sprintf "%s: internal error: %s" path (ex.ToString()))

let private test (dir: string) =
    let tmp = Directory.CreateTempSubdirectory("ns").FullName
    let mutable failed = 0
    let files = Directory.GetFiles(dir, "*.ns") |> Array.sort
    for f in files do
        let name = Path.GetFileNameWithoutExtension f
        let exe = Path.Combine(tmp, name)
        let outFile = Path.ChangeExtension(f, ".out")
        let expected = if File.Exists outFile then File.ReadAllText outFile else "<missing .out file>"
        let input = let p = Path.ChangeExtension(f, ".in") in if File.Exists p then Some(File.ReadAllText p) else None
        let result =
            match build f exe with
            | Some err -> Some err
            | None ->
                let code, out, err = run exe "" input
                if out = expected && code = 0 then None
                else
                    let el, al = expected.Split('\n'), out.Split('\n')
                    let i = Seq.zip el al |> Seq.tryFindIndex (fun (a, b) -> a <> b) |> Option.defaultValue (min el.Length al.Length)
                    let at (a: string[]) = if i < a.Length then a.[i] else "<eof>"
                    Some(sprintf "line %d: expected '%s' got '%s' (exit %d) %s" (i + 1) (at el) (at al) code err)
        match result with
        | None -> printfn "PASS %s" name
        | Some msg ->
            failed <- failed + 1
            printfn "FAIL %s: %s" name msg
    printfn "%d/%d passed" (files.Length - failed) files.Length
    if failed = 0 then 0 else 1

let private help = """ns - compiler for the NS golf language (source -> LLVM IR -> native binary)

usage: ns <file.ns>                  compile and run
       ns <command> [options] <file.ns>

commands:
  run <file>                 compile and run
  build <file> [-o <out>]    compile to a native binary (default: <file> without extension)
  ll <file> [--pure]         print LLVM IR; with --pure the runtime is included, giving a
                             self-contained module:  ns ll --pure f.ns > f.ll && clang f.ll -lm
  tokens <file>              dump stage 1: tokens
  ast <file>                 dump stage 2: surface AST
  core <file>                dump stage 3: desugared core
  typed <file>               dump stage 4: typed core (functions, captures, conversions)
  test [dir]                 run golden tests: dir/*.ns vs *.out (stdin from *.in); default ./tests
  help                       show this page

environment:
  NS3_ASAN=1                 build with Address/LeakSanitizer (checks reference counting)
"""

/// Compiles to a temp binary and runs it with this process's stdin/stdout.
let private runFile (path: string) =
    let exe = Path.Combine(Directory.CreateTempSubdirectory("ns").FullName, "a.out")
    match build path exe with
    | Some err -> (eprintfn "%s" err; 1)
    | None ->
        use p = Process.Start(ProcessStartInfo(exe, UseShellExecute = false))
        p.WaitForExit()
        p.ExitCode

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    let flag f = List.contains f args
    let option o = args |> List.pairwise |> List.tryPick (fun (a, b) -> if a = o then Some b else None)
    let positional =
        let rec go = function
            | "-o" :: _ :: rest -> go rest
            | a :: rest when a.StartsWith "-" -> go rest
            | a :: rest -> a :: go rest
            | [] -> []
        go args
    let report (path: string) (f: unit -> int) =
        try f () with
        | CompileError(line, msg) -> (eprintfn "%s:%d: error: %s" path line msg; 1)
        | ex -> (eprintfn "%s: error: %s" path ex.Message; 1)
    match positional with
    | [ path ] when path.EndsWith ".ns" -> runFile path
    | [ "test" ] -> test "tests"
    | [ "test"; dir ] -> test dir
    | [ "build"; path ] ->
        let out = option "-o" |> Option.defaultValue (Path.ChangeExtension(Path.GetFullPath path, null))
        match build path (Path.GetFullPath out) with
        | None -> 0
        | Some err -> (eprintfn "%s" err; 1)
    | [ "run"; path ] -> runFile path
    | [ "ll"; path ] ->
        report path (fun () ->
            let ll = pipeline "ll" path
            printf "%s" (if flag "--pure" then pureLL ll else ll)
            0)
    | [ ("tokens" | "ast" | "core" | "typed") as stage; path ] ->
        report path (fun () -> (printfn "%s" (pipeline stage path); 0))
    | [ "help" ] | [] when flag "-h" || flag "--help" || args.IsEmpty || positional = [ "help" ] ->
        printf "%s" help
        0
    | _ ->
        eprintf "%s" help
        2
