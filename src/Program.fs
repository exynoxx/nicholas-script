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
            let caps = if i.caps.IsEmpty then "" else " captures [" + String.concat ", " i.caps + "]"
            $"fn {i.name}({ps}){caps} -> {Core.showTy i.ret} {{\n{Core.showStmts i.body}\n  => {Core.showE i.result}\n}}")
    String.concat "\n\n" (fns @ [ "main:\n" + Core.showStmts p.main ])

/// `path:line:col: error: msg`, then the source line with a caret under the column.
let private showError (path: string) line col (msg: string) =
    let path, line = defaultArg (source line) (path, line) // an error in an imported file names that file
    let src = try File.ReadAllLines path with _ -> [||]
    if line < 1 || line > src.Length then (let loc = if col > 0 then $"{line}:{col}" else string line in $"{path}:{loc}: error: {msg}")
    else
        let text = src.[line - 1].Replace('\t', ' ')
        let col = if col = -1 then text.TrimEnd().Length + 1 else col
        let loc = if col > 0 then $"{line}:{col}" else string line
        let gutter = String(' ', (string line).Length)
        let caret = if col > 0 then $"\n{gutter} | {String(' ', col - 1)}^" else ""
        $"{path}:{loc}: error: {msg}\n{line} | {text}{caret}"

/// Runs the pipeline up to `stage` and returns its printable output.
let private pipelineSrc (stage: string) (src: string) (path: string) : string =
    let toks = Lexer.lex src 1 1
    if stage = "tokens" then toks |> List.map (fun t -> $"{t.line}: {showTok t.tok}") |> String.concat "\n"
    else
        let ast = Parser.parse toks path
        if stage = "ast" then (ast |> List.filter (function SLine _ -> false | _ -> true) |> List.map showStmt |> String.concat "\n")
        else
            let core = Desugar.desugar ast
            if stage = "core" then Core.showStmts core
            elif stage = "explain" then
                let color = not Console.IsOutputRedirected && String.IsNullOrEmpty(Environment.GetEnvironmentVariable "NO_COLOR")
                Explain.explain core (Typer.check core) color
            else
                let typed = Typer.check core |> Opt.run
                if stage = "typed" then showTyped typed
                elif stage = "repr" then Repr.describe typed
                elif stage = "c" then CGen.generate typed
                elif stage = "check" then ""
                else Codegen.generate typed

let private pipeline stage path = pipelineSrc stage (File.ReadAllText path) path

let private run (exe: string) (args: string) (stdin: string option) =
    let psi = ProcessStartInfo(exe, args, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin.IsSome)
    use p = Process.Start psi
    stdin |> Option.iter (fun s -> p.StandardInput.Write s; p.StandardInput.Close())
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, out.Result, err.Result

let private resource name =
    use s = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name: string)
    (new StreamReader(s)).ReadToEnd()

let private runtimeC = lazy (resource "runtime.c")

let private isWin = OperatingSystem.IsWindows()
/// Windows needs no libm and wants .exe names (its linker appends .exe to other names).
let private libm = if isWin then "" else "-lm"
let private exeName (path: string) = if isWin then Path.ChangeExtension(path, ".exe") else path

/// runtime.c, precompiled to LLVM IR when the compiler was built (see NS.fsproj). That IR is for the build
/// machine's C ABI, so on Windows runtime.c is compiled with the local clang instead, once per runtime version.
let private runtimeIR =
    lazy
        (if not isWin then resource "runtime.ll"
         else
            let c = runtimeC.Force()
            let hash = Convert.ToHexString(Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes c)).Substring(0, 16)
            let dir = Path.Combine(Path.GetTempPath(), "ns-runtime-" + hash)
            let ll = Path.Combine(dir, "runtime.ll")
            if not (File.Exists ll) then
                Directory.CreateDirectory dir |> ignore
                let src = Path.Combine(dir, "runtime.c")
                File.WriteAllText(src, c)
                let tmp = ll + "." + string Environment.ProcessId
                let code, _, err = run "clang" ($"-S -emit-llvm -O2 -w \"{src}\" -o \"{tmp}\"") None
                if code <> 0 then failwith ("clang failed on the runtime:\n" + err)
                File.Move(tmp, ll, true)
            File.ReadAllText ll)

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
    String.concat "\n" (Array.filter isHeader rtLines) + "\n\n; ---- NS program ----\n" + String.concat "\n" ours
    + "\n; ---- NS runtime ----\n" + String.concat "\n" (Array.filter (isHeader >> not) rtLines)

/// Self-contained C file: runtime.c followed by the program.
let private pureC (src: string) = runtimeC.Force() + "\n" + src

/// --c: build through the C backend with $CC (default cc) instead of LLVM IR + clang.
let mutable private useC = false

/// LLVM 21+ vectorises filter->fold early exits in a way that is ~10x slower for NS loops; turn it off when clang knows the flag.
let private noEarlyExitVec =
    lazy
        (let tmp = Path.Combine(Path.GetTempPath(), "ns-flagcheck.c")
         File.WriteAllText(tmp, "int x;")
         let code, _, _ = run "clang" ($"-c -mllvm -enable-early-exit-vectorization=false \"{tmp}\" -o \"{tmp}.o\"") None
         if code = 0 then " -mllvm -enable-early-exit-vectorization=false" else "")

/// The front end (desugar, typer, ...) keeps global state, so only one `pipeline` runs at a time.
let private frontLock = obj ()

/// Compiles a source file to a native binary at `exe`; returns an error message on failure.
let private build (path: string) (exe: string) : string option =
    try
      if useC then
        let cf = Path.ChangeExtension(exe, ".c")
        File.WriteAllText(cf, pureC (lock frontLock (fun () -> pipeline "c" path)))
        let asan = if Environment.GetEnvironmentVariable "NS_ASAN" = "1" then "-g -fsanitize=address" else ""
        let cc = match Environment.GetEnvironmentVariable "CC" with null | "" -> (if isWin then "clang" else "cc") | v -> v
        let code, _, err = run cc ($"-O2 -w {asan} \"{cf}\" {libm} -o \"{exe}\"") None
        if code = 0 then None else Some(cc + " failed:\n" + err)
      else
        let ll = Path.ChangeExtension(exe, ".ll")
        // whole-program module so LLVM can inline the runtime (array access, refcounts, ...)
        File.WriteAllText(ll, pureLL (lock frontLock (fun () -> pipeline "ll" path)))
        // NS_ASAN=1 builds with Address/LeakSanitizer to verify reference counting
        let asan = if Environment.GetEnvironmentVariable "NS_ASAN" = "1" then "-g -fsanitize=address" else ""
        let asan = asan + noEarlyExitVec.Force()
        let code, _, err = run "clang" ($"-O2 -w {asan} \"{ll}\" {libm} -o \"{exe}\"") None
        if code = 0 then None else Some("clang failed:\n" + err)
    with
    | CompileError(line, col, msg) -> Some(showError path line col msg)
    | :? IOException as ex -> Some($"{path}: error: {ex.Message}")
    | ex -> Some($"{path}: internal error: {ex.ToString()}")

let private test (dir: string) =
    let tmp = Directory.CreateTempSubdirectory("ns").FullName
    let files = Directory.GetFiles(dir, "*.ns") |> Array.sort
    // clang and the test binaries run in parallel; results print in file order
    let check (f: string) =
        let name = Path.GetFileNameWithoutExtension f
        let exe = exeName (Path.Combine(tmp, name))
        let outFile = Path.ChangeExtension(f, ".out")
        let expected = if File.Exists outFile then File.ReadAllText outFile else "<missing .out file>"
        let input = let p = Path.ChangeExtension(f, ".in") in if File.Exists p then Some(File.ReadAllText p) else None
        let errFile = Path.ChangeExtension(f, ".err")
        let result =
            match build f exe with
            | Some err when File.Exists errFile ->
                // an error in an imported file keeps that file's path
                let got = (if err.StartsWith(f + ":") then err.Substring(f.Length + 1) else err).TrimEnd()
                let want = (File.ReadAllText errFile).TrimEnd()
                if got = want then None else Some($"expected error\n{want}\ngot\n{got}")
            | Some err -> Some err
            | None when File.Exists errFile -> Some "expected a compile error, but it compiled"
            | None ->
                let code, out, err = run exe "" input
                if out = expected && code = 0 then None
                else
                    let el, al = expected.Split('\n'), out.Split('\n')
                    let i = Seq.zip el al |> Seq.tryFindIndex (fun (a, b) -> a <> b) |> Option.defaultValue (min el.Length al.Length)
                    let at (a: string[]) = if i < a.Length then a.[i] else "<eof>"
                    Some($"line {i + 1}: expected '{at el}' got '{at al}' (exit {code}) {err}")
        name, result
    let results = try files |> Array.Parallel.map check finally Directory.Delete(tmp, true)
    for name, result in results do
        match result with
        | None -> Console.WriteLine($"PASS {name}")
        | Some msg -> Console.WriteLine($"FAIL {name}: {msg}")
    let failed = results |> Array.filter (snd >> Option.isSome) |> Array.length
    Console.WriteLine($"{files.Length - failed}/{files.Length} passed")
    if failed = 0 then 0 else 1

let private help = """ns - compiler for the NS golf language (source -> LLVM IR -> native binary)

usage: ns <file.ns>                  compile and run
       ns <command> [options] <file.ns>

commands:
  run <file>                 compile and run
  build <file> [-o <out>]    compile to a native binary (default: <file> without extension)
  c <file>                   print self-contained C (runtime included):  ns c f.ns > f.c && cc f.c -lm
  ll <file> [--pure]         print LLVM IR; with --pure the runtime is included, giving a
                             self-contained module:  ns ll --pure f.ns > f.ll && clang f.ll -lm
  explain <file>             print the program as readable Python-style pseudocode
                             (coloured on a terminal; NO_COLOR turns that off)
  tokens <file>              dump stage 1: tokens
  ast <file>                 dump stage 2: surface AST
  core <file>                dump stage 3: desugared core
  typed <file>               dump stage 4: typed core (functions, captures, conversions)
  repr <file>                dump stage 5: the layout chosen for each type (register, storage, size)
  check <file>               type-check stdin as <file>, print errors only (editor diagnostics)
  doc                        list the stdlib: one `signature<TAB>description` line per overload (editor hovers)
  test [dir]                 run golden tests: dir/*.ns vs *.out (stdin from *.in); default ./tests
  help                       show this page

options:
  --debug                   println every assignment as `line: name = value`
  --c                       build/run/test through the C backend ($CC, default cc) instead of LLVM

environment:
  NS_ASAN=1                 build with Address/LeakSanitizer (checks reference counting)
"""

/// Compiles to a temp binary and runs it with this process's stdin/stdout.
let private runFile (path: string) =
    let tmp = Directory.CreateTempSubdirectory("ns").FullName
    try
        let exe = exeName (Path.Combine(tmp, "a.out"))
        match build path exe with
        | Some err -> (Console.Error.WriteLine(err); 1)
        | None ->
            use p = Process.Start(ProcessStartInfo(exe, UseShellExecute = false))
            p.WaitForExit()
            p.ExitCode
    finally Directory.Delete(tmp, true)

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
        | CompileError(line, col, msg) -> (Console.Error.WriteLine(showError path line col msg); 1)
        | ex -> (Console.Error.WriteLine($"{path}: error: {ex.Message}"); 1)
    useC <- flag "--c"
    Desugar.debug <- flag "--debug"
    match positional with
    | [ path ] when path.EndsWith ".ns" -> runFile path
    | [ "test" ] -> test "tests"
    | [ "test"; dir ] -> test dir
    | [ "build"; path ] ->
        let out = option "-o" |> Option.defaultValue (exeName (Path.ChangeExtension(Path.GetFullPath path, null)))
        match build path (Path.GetFullPath out) with
        | None -> 0
        | Some err -> (Console.Error.WriteLine(err); 1)
    | [ "run"; path ] -> runFile path
    | [ "ll"; path ] ->
        report path (fun () ->
            let ll = pipeline "ll" path
            Console.Write((if flag "--pure" then pureLL ll else ll))
            0)
    // check: lex, parse and type stdin as if it were <file> (imports resolve next to it); used by the VS Code extension
    | [ "check"; path ] -> report path (fun () -> (pipelineSrc "check" (Console.In.ReadToEnd()) path |> ignore; 0))
    | [ "doc" ] -> (Stdlib.docs |> List.iter (fun (sg, d) -> Console.WriteLine(sg + "\t" + d)); 0)
    | [ "c"; path ] -> report path (fun () -> (Console.Write(pureC (lock frontLock (fun () -> pipeline "c" path))); 0))
    | [ ("tokens" | "ast" | "core" | "typed" | "repr" | "explain") as stage; path ] ->
        report path (fun () -> (Console.WriteLine((pipeline stage path)); 0))
    | [ "help" ] | [] when flag "-h" || flag "--help" || args.IsEmpty || positional = [ "help" ] ->
        Console.Write(help)
        0
    | _ ->
        Console.Error.Write(help)
        2
