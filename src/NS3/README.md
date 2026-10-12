# NS3

A terse, golf-style, statically typed language that compiles to LLVM IR and then to a native binary.
Types are never written; every expression's type is inferred at compile time. Memory is reference counted.

```
1..11[~_%2]{^^2}/+                       # sum of squares of evens below 11 -> 220
1..101{print: ~_%15?"FizzBuzz":~_%5?"Buzz":~_%3?"Fizz":_}
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}   # quicksort
```

- Language: [SPEC.md](SPEC.md)
- Compiler design: [PLAN.md](PLAN.md)
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

`Lexer → Parser (surface AST) → Desugar (core) → Typer (inference, conversions, monomorphisation, closures, lowering) → Codegen (LLVM IR)`.
The program and its C runtime are compiled as one LLVM module, so runtime helpers inline.

Optimisations:
- map/filter/fold chains are fused into a single loop;
- ranges are iterated as counters;
- variable reads that are only borrowed skip refcount traffic;
- small arrays use a single allocation.

## Benchmarks

`bench/bench.sh` builds each problem in NS3, Rust (`rustc -C opt-level=3`) and Python 3. It checks that all three print identical output and reports the best of 3 wall-clock runs, in seconds.

| problem | Rust s | NS s | Python s | NS/Rust | NS/Python |
|---|--:|--:|--:|--:|--:|
| primes_sieve | 0.066 | 0.270 | 0.678 | 4.1x | 0.398x |
| primes_trial | 0.037 | 0.053 | 0.845 | 1.4x | 0.063x |
| collatz | 0.427 | 0.399 | 74.618* | 0.9x | 0.005x |
| pipeline | 0.205 | 0.329 | 20.891* | 1.6x | 0.016x |
| hashcount | 0.106 | 0.230 | 3.053 | 2.2x | 0.075x |
| fib | 0.379 | 0.376 | 20.242* | 1.0x | 0.019x |
| dijkstra | 0.247 | 0.942 | 7.199 | 3.8x | 0.131x |

\* python over 10 s: single run instead of best of 3

Measured on Linux x86-64 with clang 22, rustc 1.90 and Python 3.

Integer loops and recursion (collatz, fib, trial-division primes) run at Rust speed. The remaining gap is in allocation-heavy code: every `[d,v]` pair in Dijkstra's heap is a separate refcounted array, and hash maps go through generic descriptor-driven hashing.
