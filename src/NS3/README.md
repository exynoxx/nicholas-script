# NS3

A terse, golf-style, statically typed language that compiles to LLVM IR and then to a native binary.
Types are never written; every expression's type is inferred at compile time. Memory is reference counted.

```
1..11[~_%2]{^^2}/+                       # sum of squares of evens below 11 -> 220
1..101{print: ~_%15?"FizzBuzz":~_%5?"Buzz":~_%3?"Fizz":_}
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}   # quicksort
```

- Language: [SPEC.md](SPEC.md)
- Examples: [examples/](examples) (common patterns and LeetCode-style problems)

## Usage

Requires .NET 9 and clang. Install the `ns` command:

```shell
dotnet publish -c Release -o ~/.local/share/ns && ln -sf ~/.local/share/ns/ns ~/.local/bin/ns
```

```shell
ns prog.ns                         # compile and run
ns help
ns run examples/quicksort.ns
ns build prog.ns -o prog
ns ll --pure prog.ns > prog.ll   # self-contained LLVM module
ns test tests                     # golden tests (NS3_ASAN=1 checks leaks)
```

Every compiler stage can be printed: `tokens`, `ast`, `core`, `typed`, `ll`.

## Compiler pipeline

`Lexer → Parser (surface AST) → Desugar (core) → Typer (inference, conversions, monomorphisation, closures, lowering) → Repr (layouts) → Codegen (LLVM IR)`.
Every stage can be dumped (`ns tokens|ast|core|typed|repr|ll f.ns`). The program and its C runtime (precompiled to LLVM IR
at build time) are compiled as one LLVM module, so runtime helpers inline. Memory is reference counted.

**Representation (`Repr.fs`).** Layout is decided by rule per type: scalars live in registers; in containers a bool takes
1 byte, other scalars and heap references 8; a tuple of only scalars is a *value* (an LLVM struct, stored inline, never
boxed); other tuples and all collections are refcounted heap objects. Containers store elements inline at their storage
size; the runtime receives generic values as pointers to their storage plus a type descriptor (`I F S B A M H Q P U V`).

**Standard library.** Implemented in `runtime.c`; each function carries a `//@ name (params) result` signature that the
compiler reads (`Stdlib.fs`). Calls go through one generic path; block arguments become C callbacks with their captures.

Optimisations:
- map/filter/fold chains are fused into a single loop;
- ranges are iterated as counters;
- variable reads that are only borrowed skip refcount traffic;
- small arrays use a single allocation.

## Benchmarks

`bench/bench.sh` builds each problem in NS3, Rust (`rustc -C opt-level=3`) and Python 3. It checks that all three print identical output and reports the best of 3 wall-clock runs, in seconds.

| problem | Rust s | NS s | Python s | slowdown vs Rust | speedup vs Python |
|---|--:|--:|--:|--:|--:|
| primes_sieve | 0.064 | 0.139 | 0.616 | 2.2x | 4.4x |
| primes_trial | 0.035 | 0.050 | 0.795 | 1.4x | 15.9x |
| collatz | 0.408 | 0.430 | 71.718* | 1.1x | 166.8x |
| pipeline | 0.201 | 0.266 | 20.510* | 1.3x | 77.1x |
| hashcount | 0.104 | 0.116 | 2.869 | 1.1x | 24.7x |
| fib | 0.338 | 0.284 | 15.567* | 0.8x | 54.8x |
| dijkstra | 0.222 | 0.618 | 6.382 | 2.8x | 10.3x |

\* python over 10 s: single run instead of best of 3

Measured on Linux x86-64 with clang 22, rustc 1.90 and Python 3; all three languages in the same run (same power mode), so the ratios compare like with like.

Integer loops, recursion and int-keyed hash maps run at (or within ~10% of) Rust speed; fused pipelines within ~30%. The remaining gaps: the sieve's filter/count passes and Dijkstra's binary heap of (dist,node) value tuples, both bound by generic runtime paths (bounds checks, descriptor-driven heap compare) rather than allocation.
