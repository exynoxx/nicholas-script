<h1 align="center">NS</h1>
<p align="center"><b>the fast golf language</b><br>
terse like a golf language · statically typed · compiled to native code via LLVM</p>

---

```ns
print:1..1000[~_%3|~_%5]/+                    # 233168          Project Euler #1
print:2..50[p=_; ~2..p[~p%_]]                 # [2,3,5,7,11,…]  primes
print:1..11\*                                 # [1,2,6,24,…]    running factorials
print:(split:"the fast golf language"," ").sortby:{-#_}   # [language,fast,golf,the]
1..101{print:~_%15?"FizzBuzz":~_%5?"Buzz":~_%3?"Fizz":_}
```

```ns
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}   # quicksort
```

No type annotations, yet every expression is typed at compile time. `{}` maps, `[]` filters, `/` folds, `\` scans,
and they fuse into a single loop. Memory is reference counted; there is no GC.

**[Language spec](src/SPEC.md)** · **[Examples](src/examples)**: Dijkstra, BFS, SHA-512, top-k, quicksort, …

## Install

Needs the .NET 10 SDK and clang.

```shell
dotnet publish src -c Release -o ~/.local/share/ns && ln -sf ~/.local/share/ns/ns ~/.local/bin/ns
```

```shell
ns prog.ns                 # compile and run
ns build prog.ns -o prog   # native binary
ns ll --pure prog.ns       # self-contained LLVM IR
ns help                    # all commands, incl. dumps of every compiler stage
```

## Speed

Best of 3 runs in seconds, with all three languages measured in the same session.

| problem | Rust | NS | Python | vs Rust | vs Python |
|---|--:|--:|--:|--:|--:|
| primes_sieve | 0.064 | 0.139 | 0.616 | 2.2× slower | 4.4× faster |
| primes_trial | 0.035 | 0.050 | 0.795 | 1.4× slower | 15.9× faster |
| collatz | 0.408 | 0.430 | 71.718 | 1.1× slower | 166.8× faster |
| pipeline | 0.201 | 0.266 | 20.510 | 1.3× slower | 77.1× faster |
| hashcount | 0.104 | 0.116 | 2.869 | 1.1× slower | 24.7× faster |
| fib | 0.338 | 0.284 | 15.567 | 1.2× faster | 54.8× faster |
| dijkstra | 0.222 | 0.618 | 6.382 | 2.8× slower | 10.3× faster |

Sources and runner: [`src/bench`](src/bench).
