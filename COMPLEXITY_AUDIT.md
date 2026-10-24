# What to cut from the NS compiler

About 1,200 lines of the compiler's ~5,900 can go without losing anything that matters. Most of them sit in a few
features that nothing outside their own single test uses: no example program, no benchmark, and not `nsc`.

This is a research report: no source files were changed. Line numbers refer to commit `5712f59`.

## The big wins

**1. Drop `Codegen.fs` and keep the C backend (`CGen.fs`). Saves about 560 lines.**
- The two files are line-for-line twins: same functions in the same order (`ex`, `exB`, `binop`, `call`, `stdCall`,
  `stmt`, …), with `noAssign` and `lendable` copy-pasted. All 7 backend commits since the C backend landed (a6fc5b3)
  touched both.
- The C backend passes all 103 tests and 15 examples (`--c`).
- Built with `CC=clang`, it is as fast or faster on 7 of the 8 benchmarks (times in seconds, best of 3):

  | bench | LLVM IR | C backend via clang | C backend via gcc |
  |---|--:|--:|--:|
  | primes_sieve | .162 | .155 | .119 |
  | primes_trial | .052 | .052 | .116 |
  | collatz | .453 | .445 | .793 |
  | pipeline | .278 | .243 | .188 |
  | hashcount | .375 | .295 | .287 |
  | fib | .357 | .341 | .468 |
  | dijkstra | .717 | .723 | .539 |
  | dfs | 1.259 | 1.478 | 1.792 |

- `dfs` is 17% slower. The likely cause is that the C build doesn't get the `-enable-early-exit-vectorization=false`
  flag the LLVM build gets, but that couldn't be confirmed because the rerun was killed. `$CC` is a bare program name
  today, so it can't carry that flag.
- Also removed:
  - the `pureLL` and `runtimeIR` code in `Program.fs`
  - the `CompileRuntime` target and the `runtime.ll` file in `NS.fsproj`
  - the `ns ll` command, which `clang -S -emit-llvm` on the `ns c` output replaces.
- This also halves the cost of every future backend change. `nsc` writes its own LLVM IR, so it isn't affected.

**2. Graphs `G[..]` / `DG[..]`. Saves about 170 lines.**
- Where:
  - `runtime.c:1144-1279`, plus the `G` cases in `ns_eq`, `tostr`, `ns_toarr`, `ns_has` and `skip`
  - `isEdge` and the operator overloads in `Typer.fs` (123-129, 294-309, 349, 380, 388)
  - `TGraph` in `Core.fs` and `Repr.fs`, `SGraph` in `Stdlib.fs`, the graph cases in `Lower.fs`
  - the `G[`/`DG[` token in the lexer, and §graphs in `SPEC.md`.
- Whether `g+x` means a node or an edge is decided by type, and that check changes the types it looks at as a side
  effect (`isEdge` unifies holes).
- Used by 1 test (`m7_graphs`), plus 2 lines in `m7_stdlib_colls`. Dijkstra, BFS and dfs all use adjacency maps
  instead.

**3. Sorted tree `BT`. Saves about 130 lines.** The cheaper option is to drop only the keyed form `BT:xs,{key}`, about
40 lines.
- Where: `runtime.c:370-455`; `Typer.fs` 59-60, 401-418 and 493-508 (`treeOp`); `Core.fs:17`; `Repr.fs`;
  `Codegen.fs:319` and `CGen.fs:302`.
- The keyed form puts a block id inside the type (`TTree of Ty * (int*Ty) option`). That forces special handling in
  `unify`, a special case in `stdCall` (`Typer.fs:481`), and its own `K` descriptor in the runtime.
- Used by 1 test (`m7_btree`). `PQ` and `sort` cover most of what it's used for. Nothing cheap replaces ordered delete
  or max.

**4. Opt.fs: two passes that do nothing or almost nothing. Saves about 150 lines (19% of `Opt.fs`).**
- **Static-argument hoisting (`satInst`, lines 377-475) never fires.** The typer passes parameters as `xs' = copy(_1)`
  and recursive calls as `copy(xs')`, so no argument ever looks unchanged to it. Its own case in `m8_licm.ns`
  (`hits xs k i`) passes only because hoisting doesn't change the result. The tail-recursive case is already handled by
  `tailLoop` followed by LICM.
- **Pure-call CSE (`cseE`/`cseStmts`, lines 546-590) fires only in its own test** (`m8_licm`). A contrived benchmark that
  calls `xs.sort` twice per iteration was about 20% faster with it on. Users rarely repeat a pure call in straight-line
  code.
- Neither pass needs any code in Codegen, CGen or runtime.c.
- Caveat: the golden tests were rerun with all passes on and with hoisting off. CSE-off was checked only on
  microbenchmarks.

## Medium wins

| # | Feature | Lines | Used by | What users write instead |
|---|---|--:|---|---|
| 5 | Match filter `?[..]`, and a match over an array automatically mapping over it | ~40 | 1–2 tests | `xs[_ ?{…}]`, `xs{_ ?{…}}` |
| 6 | Negated functions as values (`g = !ws`, `!{..}`) | ~25 | 2 tests | `g = {!ws:_}` |
| 7 | `coll ** 0` empties a collection in place (`keep`) | ~40 | 1 test | `s = %[]` |
| 8 | Sort by key `x${key}` | ~18 | 3 tests | `xs.sortby:{key}`, which already sorts stably |
| 9 | Builtins that are only wrappers around runtime calls, written in both backends (`len`, `has`, `slice`, `add`, `del`, …) | 40–60 | none lost | route them through the `//@` signatures (already a TODO at `Opt.fs:47`) |
| 10 | Stdlib aliases that survive shadowing (`m = max`, then `max = 3`) | ~15 | 1 test | `m = {max:_}` |
| 11 | Tuple-unpacking parameters `f (a,b) (c,d) = …` | ~15 | 1 test | `a,b = p` in the function body |

More about some of these:

- **#5**
  - Needs a raw-text lookahead in the lexer (`ternaryArray`, `Lexer.fs:83-95`) to tell `?[..]` apart from a ternary
    `c ?[1]:[2]`.
  - Changes meaning when one pattern is an array literal: then the match compares the whole array instead of mapping.
  - The `MatchOn` Core node, which appears in 6 files, exists only to support it. Without it, a match turns into a
    plain `Seq` in Desugar.
  - Removing match entirely saves about 90 lines. It is used in only 4 test files.
- **#6**
  - Is the third name prefix (`negMark`) encoded into call names.
  - Binds a hidden variable `not'{id}` in `SectionBlock`.
  - `s[!ws]` inside a filter or map block could stay, since it is just `!ws:_`.
- **#7** makes `**` behave differently by type: it copies arrays and strings but empties sets, maps, Q and PQ in place.
- **#8** makes `$` context-sensitive in the lexer: after an expression `${` is a sort, otherwise `$` is the fold
  accumulator.
- **#9**
  - The duplication sits in `Codegen.fs:309-361` and `CGen.fs:270-339`.
  - Keep only the builtins that emit real inline IR: `neg`, `not`, `tuple`/`tget`, `push`/`append`/`copy`, `mapof`.
  - The catch: `add`, `del` and `merge` currently return their first argument with ownership, so they would need to
    return int, with the typer wrapping the call in a `Seq` the way `treeOp` already does.
- **#10** threads a `stdAliases` dictionary through the `Parser` constructor and every sub-parser. Aliasing a *user*
  function is one line and can stay.

## Small items

- **Dead code:** `runtime.c:278-294` (`zero()` and its forward declarations) is never called, and `"zero"` in
  `Opt.fs:40` can never be reached.
- **Implicit `[a]→[b]` element conversion** (`castName`, `Typer.fs:109-115`; `convArr`, `Lower.fs:132-135`; about 12
  lines) is used by none of the 118 programs. Without it, array type mismatches become compile errors instead of
  hidden loops.
- **Duplicate syntax:**
  - Arm blocks `=> {..}` duplicate `=> (..)`.
  - `(a,b) = t` duplicates `a,b = t`.
  - `t._1` duplicates `t@0`.
- **`ns_eq` repeats `ns_cmp`** (`runtime.c:191-224`) for the ordered kinds, about 15 lines. Check how NaN compares before
  merging them.
- **Collection literal tokens `Q[ PQ[ BT[ G[ DG[`** duplicate `PQ:[]`, `set:[]` and `G:[(1,2)]`, which already work.
  Removing them frees five names and drops a lexer rule, about 8 lines. Low priority, because real programs use them
  and about 15 files would change.

## Simplify rather than cut

- **Join of a loop in the string builder** (`joinLoop`, about 25 lines) makes `xs{_*2}.join:","` 1.4–2.3× slower on
  int elements, measured twice with noisy timings. It helps string elements (`xs{"<"+_}.join`: 0.78s vs 1.99s). Fire
  it only when the elements are strings.
- **In-place reuse (`reuse`, `Opt.fs:271-305`)** works by matching the exact statement shape `Lower` emits, so any
  change to `Lower` can silently turn it off. It belongs in `Lower.iter`.
- **Dead pure code (`dce`, `Opt.fs:592-631`)** mostly removes unused int temps that LLVM drops anyway. If `Lower` stopped
  binding `_`/`@` when a block never reads them, the pass could go.
- **Effect tables (`pureStd`/`totalStd`, `Opt.fs:37-53`)** duplicate what runtime.c knows and will drift from it. Move
  them into `//@ pure` / `//@ total` markers parsed by `Stdlib.fs`.
- **`inferBorrows`'s comment** says it lets LLVM's tail-recursion elimination work. `tailLoop` made that reason obsolete,
  so drop the sentence.

## Worth keeping

These were checked and pull their weight:

- **Kept for speed:**
  - `tailLoop`: over 60× faster on a 1M-element tail-recursive walk. LLVM can't do this itself, because releasing an
    owned parameter after the call blocks it.
  - LICM: big win on pure calls inside loops.
  - The `s = s + e` builder: turns quadratic into linear (200k appends: 0.014s vs 42s).
  - The 3+-part `+` builder: fires in 20 files.
  - Borrowed parameters: about 14% on non-tail recursion.
  - Value vs boxed tuples, the 1-byte bool storage (`esz`), and inline element access.
- **Used by real programs:** Q, PQ, `m@k op=`, `??=`, broadcasting, `conv`, `memo:`, operator sections, `/op` folds,
  interpolation.
- **Foundational:** the `isFunc` call rule (`f:args` is a call only when `f` is known to be a function) is the real
  root of the front-end's complexity, so it stays. Items 6 and 10 remove parts of the machinery it needs.

## Suggested order

1. Dead code: `satInst`, `zero()`, the `"zero"` entry.
2. Cut CSE.
3. Drop `Codegen.fs`.
4. Cut `G` and the keyed form of `BT`.
5. Cut the front-end sugar (#5, #6, #8, #10, #11).

Taken together that is roughly 1,200 lines. Each cut removes its feature's test, its section in `SPEC.md`, and the
matching code in `nsc`.
