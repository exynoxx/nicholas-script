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

New feature → add `tests/<milestone>_<name>.ns` + `.out`. `runtime.c` is compiled to `obj/.../runtime.ll`
by the `CompileRuntime` target in `NS.fsproj`.

## Pipeline (one file per stage)

| stage | file | in → out | look here for |
|---|---|---|---|
| 1 | `Lexer.fs` | text → `Token list` | every context-sensitive decision: `m{` map vs `{` block, `f[` filter vs `[` literal, `/{` `\{` fold/scan, `FOLDOP`/`SCANOP` (`/+`), `~{` while, `-` number vs minus, `#` comment vs length, operator sections (`sectionOps`, inserts `_`), newline continuation |
| 2 | `Parser.fs` | tokens → surface AST (`Ast.Expr`/`Stmt`) | recursive descent, one member per precedence level (`Expr` ternary → `Or` … `Pow` → `Prefix` → `Postfix` → `Primary` → `Base`). `f:args` calls only when `isFunc` (builtins + `Stdlib.names` + names assigned a block). `.method`, `@` index, `import` splicing, `SectionBlock` (`x{int}` = `x{int:_}`), `RangeEnd` (`0..n-1`), `GroupAhead` (`(s1; s2)` → `EGroup`), else-less ternary in `Expr` |
| — | `Ast.fs` | | token + surface AST types, `CompileError`/`fail`, printers |
| 3 | `Desugar.fs` | AST → `Core` (untyped) | type-free sugar: interpolation → `+`, ranges/slices → calls, `[x]*n` → `repeat`, parallel assign via temps, `_ @ $` renamed to fresh per-block vars, fold ops `/>` `/<` → `If` |
| — | `Core.fs` | | core IR: `Ty` (with `THole` unification vars), `E`/`K`, `S`, `Iter` |
| 4 | `Typer.fs` | core → typed core (`Typer.Program`) | inference + unification, left-operand conversion (`conv`, `typeBin`), scopes/captures (`Frame`, `propagateCaptures`), monomorphising blocks per call site (`userCall`, `Inst`), primitives in `builtin` (`print`, casts, `neg`, `len`, `pop`, `keys`…), runtime stdlib in `stdCall` |
| — | `Lower.fs` | | type-directed rewrites the typer invokes: `iter` (fuses map/filter/fold/scan chains into one loop), `broadcast` (array arithmetic), `convArr`, `setOp` |
| 5 | `Repr.fs` | | memory layout per type: `llty` register type, `storage`/`esz` in containers, `desc` runtime descriptor, value vs boxed tuples |
| 6 | `Codegen.fs` | typed core → LLVM IR text | `generate`; refcount retain/release, inline element access |
| — | `Program.fs` | | CLI (`main`), `pipeline` stage dumps, `build` (clang), `test` runner, `help` |

## Runtime and stdlib

- `runtime.c` — the whole runtime: refcounted heap objects with a 16-byte `{rc, descriptor}` header, descriptor
  strings (`I F S B A<t> M<k><v> H<t> Q<t> P<t> U.. V..`, documented at the top), arrays, hash map/set,
  Q/PQ, strings, print/tostr, sort, stdlib functions.
- **Adding a stdlib function** = write it in `runtime.c` with a `//@ name (params) result [= c_symbol]` comment.
  `Stdlib.fs` parses those comments; the parser and typer pick it up automatically. No F# change needed.
- Typer-only primitives (`neg`, `len`, …, in `Typer.builtin`) are *not* in `Parser.builtins`, so users can't
  call them by name.

## Other dirs

- `src/examples/` — larger programs with `.out` (and `.in`) files; also golden tests.
- `src/bench/` — NS vs Rust vs Python, `bench.sh`.
- `src/tests/lib/` — files used by the import test.
