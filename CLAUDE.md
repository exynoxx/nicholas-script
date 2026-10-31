# NS compiler — navigation

F# (.NET 10) compiler for the NS golf language: source → LLVM IR → native binary via clang. All code in `src/`;
files compile in the order listed in `src/NS.fsproj`. User-facing language reference: `src/SPEC.md` (keep it in
sync with syntax changes); tour: `README.md`.

## Build / test

```shell
cd src
dotnet run -c Release -- test tests        # golden tests: tests/*.ns vs *.out (stdin from *.in)
dotnet run -c Release -- test examples     # bigger programs (Dijkstra, BFS, ...)
dotnet run -c Release -- <stage> f.ns      # tokens | ast | core | typed | repr | ll — dump one stage
NS_ASAN=1 dotnet run -c Release -- f.ns    # ASan/LSan: checks reference counting
```

The `ns` on PATH is an installed copy (`~/.local/bin/ns` → `~/.local/share/ns/ns`), not this build: after every
feature, reinstall it with `dotnet publish src -c Release -o ~/.local/share/ns` (from the repo root).

New feature → add `tests/<milestone>_<name>.ns` + `.out`. A `.err` file instead expects a compile error
(the message after `path:`, with the source line and caret); see `tests/e_*`. `runtime.c` is compiled to `obj/.../runtime.ll`
by the `CompileRuntime` target in `NS.fsproj`.

## Layout: kernel + features

The compiler is split into a **kernel** (`src/kernel/`, one file per stage, knows no feature) and **features**
(`src/features/`, one file per language construct or compiler mechanism). Each stage exposes hook tables; a feature is
a `Feature.Feature` record (`src/Feature.fs`) filling the hooks it needs, registered in order by `src/AllFeatures.fs`
(order matters for list hooks: first match wins). Compile order in `NS.fsproj`: kernel → `Feature.fs` → features →
`AllFeatures.fs` → `Program.fs`; a later feature may call an earlier one directly (`Lazy` → `Fusion.typeIter`).

| stage | kernel file | hooks a feature fills |
|---|---|---|
| 1 | `Lexer.fs` | `lex` rules (get a `Lexer.Ctx`), `openers` (sections), `closers` (tokens ending an expression) |
| 2 | `Parser.fs` | `prefix`/`postfix`/`suffix` forms by token, `stmtPrefix`, `assignRhs`, `fnStarts`, `modifiers`, `builtins`; hooks get the `Parser` object (public members: `Expr`, `Block`, `Peek`, ...) |
| — | `Ast.fs` | features produce `EExt(name, operands, blocks)` (or existing nodes) |
| 3 | `Desugar.fs` | `desugar` per `EExt` name (gets a `Desugar.Api`), `corePasses` (whole program), `perElement` |
| — | `Core.fs` | `Ty.TCon(name, args)` for feature types (`types`: show + runtime descriptor) |
| 4 | `Typer.fs` | `intercept` (untyped nodes), `prims` (typed calls by name), `bin`, `index`, `freshness`, `assignRewrite`, `bodyRewrite`, `noSpread`, `fixerExempt`, `prologue`, `reset`; the typer's functions are public |
| 4b | `Opt.fs` | `effects` of primitives, `optPasses` (`Opt.Pass`) |
| 5 | `Repr.fs` | (descriptors via `types`) |
| 6 | `Codegen.fs` | `llvm` per primitive (gets a `Codegen.Api`), `llvmRaw`, `kept` |

Features: `fusion/` (iteration typing + the loop engine `Lower.fs` + the chain-moving core pass), `Ranges`, `Arrays`,
`Strings`, `Tuples`, `Maps`, `Coll` (shared by `Set`, `Queue` (Q, PQ)), `Tree` (BT), `Sort` (`${`), `Interp`, `Match`
(`?{` `?[`), `Fold` (`/op` `/{`), `Try` (`?? stmt`), `Memo`, `Lazy`, `Debug` (`--debug`), `opt/` (one file per pass).
Still in the kernel: map/filter/while syntax, `..` parsing, `x'` state, base cases, sections, destructuring,
scopes/captures, monomorphisation, conversions, refcounting, layouts. `Program.fs`: CLI, `pipeline` stage dumps,
`build` (clang), `test` runner, `help`. (The C backend and `explain` were removed on this branch.)

## Runtime and stdlib

- `runtime.c` — the whole runtime: refcounted heap objects with a 16-byte `{rc, descriptor}` header, descriptor
  strings (`I F S B A<t> M<k><v> H<t> Q<t> P<t> T<t> K<k><t> U.. V..`, documented at the top), arrays, hash map/set,
  Q/PQ, strings, print/tostr, sort, stdlib functions.
- **Adding a stdlib function** = write it in `runtime.c` with a `//@ name (params) result [= c_symbol]` comment. Follow it with a `//? one-line description` (shown in VS Code hovers via `ns doc`).
  `Stdlib.fs` parses those comments; the parser and typer pick it up automatically. No F# change needed.
- Typer-only primitives (`neg`, `len`, …, in `Typer.builtin`) are *not* in `Parser.builtins`, so users can't
  call them by name.

## Self-hosted compiler (`src/nsc/`)

`nsc` is an NS compiler written in NS (lexer, parser, typer+codegen to LLVM IR; clang links it with `runtime.c`).
`src/nsc/bootstrap.sh` builds it with `ns` and then with itself (stage 2 and 3 IR must be identical);
`src/nsc/test.sh` runs the golden tests through it. Read `src/nsc/README.md` before editing: it lists the NS
gotchas that matter when writing a large NS program.

## Other dirs

- `src/examples/` — larger programs with `.out` (and `.in`) files; also golden tests.
- `src/bench/` — NS vs Rust vs Python, `bench.sh`.
- `src/tests/lib/` — files used by the import test.
- `src/vscode/` — VS Code extension: grammar, live error squiggles via `ns check`, hovers via `ns doc` (stdlib) and `ns types` (variable types, recorded in `Typer.hovers`) (symlink it into `~/.vscode/extensions/ns-lang`).
