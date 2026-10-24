# NS compiler fuzzing audit — 2026-10-20

Eight agents wrote about 1,000 small NS programs to find unanticipated combinations of input. They covered:

- lexer and parser
- newlines and whitespace
- the typer
- collections and iterator chains
- control flow and `Opt`
- stdlib, strings and IO
- reference counting under ASan
- compile-error quality

Each program ran on the LLVM backend and through `--c`. Suspicious ones also ran under `NS_ASAN=1`. No `src/` files were changed.

**Status (2026-10-21).** ✅ = fixed on master (commit given), ◐ = partly fixed, unmarked = still open.
Fixes are one commit each, with the cause and fix in the message and a golden test.

**Build tested.** Most agents used a clean build of 5712f59; the working tree didn't build because of the in-progress `Codegen.fs:259`. Findings marked **[HEAD]** were also confirmed on de89110. The ten headline reproducers were re-run by hand on both builds.

**Reproducers.** They are in `/tmp/claude-1000/-home-nicholas-Dokumenter-git-nicholas-script/b5c1c624-5db2-4397-a87e-5a377fb0b352/scratchpad/{lexer,ws,types,colls,ctrl,stdlib,mem,errors}/`. The scratchpad is temporary: copy anything you want to keep into `tests/`.

**Healthy areas.**
- The LLVM and C backends never disagreed, except on float/int undefined behaviour (see §4).
- ASan found no use-after-free or leaks except the one in §1.1.
- The golden tests and examples pass under ASan on both backends.
- Fusion, FBIP, `.take`, string building, tail calls, borrowed parameters and IO edge cases (empty stdin, a 3 MB line, CRLF files) all held up.

---

## 1. Wrong output: serious

### ✅ 1.1 Arrays taken out of containers are shared instead of copied (value semantics) [HEAD]
✅ 7a0c1df (`own` copies any non-fresh source), c6acc01 (`ns_sortby` length). Still open: `[[0]] ++ xs` shares `xs`'s inner arrays; a map body that changes `_` in place over an array of arrays changes the source.
`Typer.own` (~Typer.fs:117) inserts `copy` only for a bare `Var`. In-place ops (`v@i =`, `v ++=`, `v = v{f}`, `v += 1`, map write-back) assume a single owner and never check the refcount.
```
xs = [[1,2]]
v = xs@0
v@0 = 7
println:xs        # [[7,2]]   expected [[1,2]]
```
- **Same bug via other sources:** map values `m@k`, tuple fields and destructuring (`a,b = t`, tuple parameters), `q@0`, slices `xs@0..1`, ternary, match, group `(0; xs)`, a block returning a capture, `id:a`, the identity map `a{_}`, folds `a/{$}`, and `m@1 = m@0`.
- **Not affected:** filter, sort, rev, take, scan, and plain `b = a`.
- **Corrupts hash keys:** `m=[([1],0)->"v"]; a,b = m.keys@0; a@0 = 5` makes the key unfindable under both the old and new value. Sets do the same: `s=%[[1,2]]; v=(arr:s)@0; v@0=5`.
- **Corrupts the memo cache:** `r = f:"abcde"; r@0 = "Z"` changes later calls to `f`.
- **Memory error:** `xs=[[3,1,2]]; v=xs@0; println:(xs@0).sortby:{v = v ++ 0; _}` gives ASan SEGV on LLVM and heap-buffer-overflow on C. `ns_sortby` (~runtime.c:1108) sizes `keys` from the original `a->len` but loops on the live length.
- **Map keys too:** `m@a = 1` (also `+=` and `??=`) stores array key `a` without `own`, so `a@0 = 9` afterwards breaks the map invariant.
- **Fix:** copy any non-temporary array source in `own`, or check the refcount before an in-place write.

### ✅ 1.2 Regression in 15969e6: LICM and CSE ignore globals and captures read inside callees [HEAD only]
`Opt.pureInputs` now collects only the expression's own `Var`s, not the callee summary's `s.reads`.
```
g = 0
f x = x + g
i = 0
i < 3 ~{ println: f:1; g += 1; i += 1 }    # HEAD: 1 1 1   5712f59: 1 2 3
```
- CSE has the same bug: `a = f:1; g = 5; b = f:1` gives `b == a`. Closures behave the same way.
- **Fix:** union the inputs with `s.reads`.
- ✅ 05ae297

### ✅ 1.3 Tail-call loop plus LICM returns the wrong base case [HEAD]
✅ bbad997
```
parity n = (n = 0) ? "even" : (n = 1) ? "odd" : parity: n - 2
println: parity: 10       # odd
```
After `tailLoop`, each base branch becomes `(tail%r = "even"; 0)`. That only writes a temp, so LICM hoists both assignments out of the loop and the last one wins. It also happens with match arms and pure calls in the arms.

### ✅ 1.4 `pop` and in-place `**` don't record a write, so LICM and CSE see stale collections [HEAD]
✅ 2c3c0f4
```
q = PQ[5,3,9]
s = []
#q > 0 ~{ s = s ++ (sort: arr: q)@0; q.pop }
println: s                # [3,3,3]   expected [3,5,9]
```
- Same with a BT: `a = t.min; t.pop; b = t.min` gives `b == a`.
- Same with `m ** 0` and `s ** F`.
- **Cause:** a non-pure StdCall sets `effect` but records neither `refWrite` nor a write. This affects Dijkstra-style loops.

### ◐ 1.5 A recursive function's return type comes from the recursive call, not the base case
◐ 0eab2b1. Still wrong when the recursive branch comes first: `n>0 ? (r:n-1)+1 : 0.5`.
When the recursive call is the left operand, the unknown result type takes the right operand's type, and the base value is converted to it.
```
f n = n<=0 ? 0.5 : (f:n-1)+1      # f:2 -> 2     expected 2.5
f n = n<=0 ? "" : (f:n-1) + n     # f:2 -> 3     expected "12"
f n = n<=0 ? [1.5] : (f:n-1) ++ [n]   # [1,1,2]
```
- Mutual recursion and the block form `g = {…}` fail the same way.
- A tuple result gives "cannot convert int to (int,int)".
- Mapping a recursive result, then `++`, gives "cannot convert [[str]] to BT str".

### ✅ 1.6 A block nested in a function called at two types reuses the first instance (segfault)
✅ 1a21ccf
The inner block's instance is cached by argument types only, so the second instantiation calls a block that captures the first one's variables.
```
outer x = {
  c = [0]
  push = {c ++= _}
  push:1
  #c
}
println:outer:10
println:outer:0.5        # SEGV on both backends
```
With a scalar capture it gives wrong output instead: `outer x = {c = x; bump = {c += 1}; bump:0; c}` gives `outer:0.5` = 0.5, expected 1.5.

### 1.7 Plain `& | -` and `+ -` on set, map, Q and PQ change their left operand
```
a = %[1,2,3]; b = %[2,3,4]
c = a & b
println:a            # %[3,2]
```
- Even `println:#(s & %[5])` empties `s`.
- `scombine` (~runtime.c:1284) works in place. SPEC §3 says only `op=` mutates, and graph `| & -` already return a new value.

### ✅ 1.8 `sum` and `prod` accept any element type and read it as int64 [HEAD]
✅ 4a570d8 (non-numeric elements are a compile error)
- `sum:["a","b"]` and `sum:[[1],[2]]` segfault.
- `prod:[T,T,T]` gives `F` (bools are 1 byte, so it reads garbage).
- `sum:[(1,2),(3,4)]` gives `(3,0)`; `/+` correctly gives `(4,6)`.
- **Fix:** reject non-numeric element types in the typer.

### ✅ 1.9 Size overflow in allocations
✅ 375362c (panics with `out of memory`)
`ns_repeat` (`n*k`) and `ns_arr_new` (`cap*sz`) can wrap:
- `"abcd" ** 4611686018427387904` segfaults.
- `[7]**2305843009213693952` and the materialised `0..2305843009213693952` give an ASan heap-buffer-overflow.

### ✅ 1.10 Other wrong output
| Repro | Actual | Expected / cause |
|---|---|---|
| ✅ `xs@0 = grow:0` where `grow` reassigns `xs` | the write goes to the old array | the target is read before the RHS runs; ✅ c358c95 |
| ✅ `f = { a = _1; _2 = 0 ? _1 : (a@0 = a@0 + 1; f: a, _2 - 1) }; f:[5],3` | `[5]` | `[8]`: `tailLoop` alias check only scans `i.body`, not `i.result`; ✅ f02226b |
| ✅ `h x = 10/x; z=0; (println:"check"; i < (h:z)) ~{…}` | panics before printing "check" | LICM hoists the condition part past an earlier effect; ✅ 57aba86 |
| ✅ `xs=[[1],[2]]; xs{_@@}` | no error (statement deleted) | index out of range; `sumE` treats renamed `_` and `@` temps as in bounds; ✅ 7d7ad45 |
| ✅ `println:[1,2]+[1,2,3]` | `[2,4]` | runtime error (SPEC); the reverse order does error; ✅ 308c459 |
| ✅ `k{(_ = 10)}` on a map | writes back | parens dropped, so the equality is treated as an assignment; ✅ 8fb3da5 |
| ✅ `m{=2}` on a map | overwrites `m` | the section `_ = 2` turns into a write-back; on arrays the same code gives `[F,T,F]`; ✅ 8fb3da5 |
| ✅ `println: 3 > 2 > 1` / `println: 1 = 2 = F` | `T` / `F` | `ECall(println,[3>2]) > 1`; a chained compare at top level is rejected; ✅ 26f7cb0 (now an error) |
| ✅ `println:0..10..2` | prints 0..9 | `range(println(range(0,10)), 2)`; `x = 0..3..100` is rejected; ✅ 03a8e54 (now an error) |
| ✅ `println: [[1,2]]{_0}` | `[2]` | error; `_0` becomes `_@-1`; ✅ dc58527 (now an error) |
| ✅ `1^^-1`, `(-1)^^-1` | 0, 0 | 1, -1 (`ns_ipow` returns 0 for `e<0`); ✅ db1cd22; `0^^-1` errors: 7bf513c |
| `25 ^^ 0.5` (int base, float exponent) | `1` | `5.0`: the left-operand rule converts 0.5 to int 0; also breaks the SPEC `dist` example (§7). Workaround: `float:` the base. Found 2026-10-24 |
| ✅ `abs:INT64_MIN`, `gcd:INT64_MIN,6` | 0 / 6 on LLVM, INT64_MIN / -2 on C | undefined behaviour in `-x`; the answer is 2 for gcd; ✅ 2a6501f |
| ✅ `x = 10\n  - 1` / `ys = xs\n  {_*2}` | 10 / `[1,2]` | the indented line silently becomes its own statement; a leading `+` is rejected; ✅ 9d5ce53 |
| ✅ `xs[s = _@1..\n #s]` (open slice at line end) | joins the next line | `..` without an end isn't a binary operator; ✅ 6827390, d9abfda |
| ✅ `c = 0; [1,2,3]{c += 1; c}{_ * c}` | `[1,4,9]` | fusion interleaves effects; SPEC is silent; ✅ c4662f9 (documented in SPEC) |

## 2. Compiler crashes and hangs
| Repro | Result |
|---|---|
| ✅ `println:99999999999999999999`, `0xfffffffffffffffffff` | `OverflowException` in `Lexer.lexNumber`; ✅ ab6b9f2 |
| ✅ `println:0x`, `0b2`, `0xG` | `ArgumentOutOfRangeException` in `Convert.ToUInt64`; ✅ ab6b9f2 |
| ✅ `println:٣` (any non-ASCII Unicode digit) | `FormatException`: `Char.IsDigit` accepts it but `Int64.Parse` rejects it; ✅ ab6b9f2 |
| `f = {[_]}; g = {f:(g:_)}; println:g:1` | typer never terminates (infinite type) |
| `p xs = #xs < 2 ? [xs] : xs{x = _; (p:xs@1..)}/{$ ++ _}` | typer never terminates |
| `5.panic` | invalid IR: `call void @ns_panic(ptr 5)` (C backend also fails) |
| 3000 nested parens, or about 10k terms in one expression | stack overflow (Parser / `Opt.sumE`) |

## 3. Accepted when it should be rejected
- ✅ **Extra call arguments are silently dropped.** (4f2fa02; too few arguments: ea237b2) `inc x = x+1; println:[inc:1, 5]` gives `[2]`, and `(f:0,0, f:0,1)` becomes `f(0,0,f(0,1))`. This is the most-hit trap in the whole run: four agents found it independently.
- **Internal primitives are reachable as methods.** `"a".len`, `5.neg`, `T.not`, `5.range:9`, `"ab".repeat:3`, `(1,2).tuple` and `"x".entries` all compile.
- ✅ **Blocks as values.** (98017db, 201dfe5)
  - `println:f`, `println:{7}` and `c ? {7} : {8}` print `0`.
  - `(f,1)` is allowed inside a tuple.
  - `g = {{_+1}}`, and `mk` returning an inner block, let a block escape.
- **Duplicate parameters:** `f a a = a` is accepted.
- **Empty fold body:** `[1,2]/{}` gives `1`.
- **Inference gaps:** `f = {f:_}` infers result type `?` and then loops forever at runtime.
- ✅ **SPEC rule not enforced:** (91a7333) "a use inside a block called at two different types is an error" isn't checked; `add:"x"; add:2` converts instead.
- **Reserved names:**
  - `Q = 5` is accepted, although SPEC says `Q` and `PQ` are reserved.
  - `G`, `DG` and `BT` aren't reserved, so `G = [1,2,3]; G[>1]` is lexed as a graph literal.
- **`&` and `|` on arrays and sets** fall back to truthiness: `[5,6]&[1,2]` gives `T`, while `^`, `<<` and `>>` work per element.
- ✅ **Silent no-op statements:** (346ecdb) `3 = x`, `(x) = 3` and `T = 5`.
- ✅ **Float literals:** (ab6b9f2) a literal that overflows silently becomes `inf`.

## 4. Backend mismatches (all float/int undefined behaviour)
- **NaN sign:** `0.0/0` prints `nan` when LLVM constant-folds it, and `-nan` on C and at runtime. Fix: print `nan` regardless of sign in `fmt_float`.
- **Out-of-range or NaN float→int:** `int:(10.0^^30)` and `int:nan` give `0` on LLVM and `INT64_MIN` on C (undefined `fptosi`).
- ✅ **`abs` and `gcd` at INT64_MIN:** see §1.10 (2a6501f).

## 5. Valid programs rejected
- ✅ **Fold or scan followed by an operator:** (c48b77a) `xs/+ * 2`, `xs/+ > 5`, `xs/+ = 6` and `xs\+ ++ [0]` fail. `isTerminator` in `tryOpAfter` doesn't include operator characters.
- ✅ **Negated length after `/`:** (c48b77a) `10/-#x` and `10/-.5` are lexed as a `/-` fold.
- ✅ **Match filter in a ternary:** (321c645; every `?[ ]` arm must give a truthy/falsy value: 08c188e) `T ? xs?[>4] : xs` is seen as a ternary with an array literal (`ternaryArray`).
- ✅ **Continuation after a multi-line bracket closes:** (61e869e) `xs = [\n  1, 2] ++\n  [3]` fails because `stmtIndent` is never restored.
- **A blank line, or a column-0 comment, inside a continuation** ends the statement (`x = 1 +\n\n  2`). A whitespace-only line works.
- ✅ **Fold or scan `{` on the next line:** (3fa50e4) `xs /\n  {$+_}` is lexed as division by a block, with a misleading error.
- **Mixed tabs and spaces:** 4 spaces followed by a tab continuation is rejected; tab followed by 4 spaces is accepted. A tab counts as one column.
- ✅ **Shadowed builtin as a ternary branch:** (09fd449) `max = 3; T ? max : 0` says "'max' is a variable here". `max = 3.5; m = max` says "undefined variable 'm'".
- ✅ **Block passed to a user function:** (6303bec) `apply f x = f: x` says "'f' is not a function". SPEC doesn't say whether this is allowed.
- **Queue/PQ `-` element:** rejected, but listed in the SPEC operator matrix.
- **Scan over Q, PQ, set or map:** gives the internal error "push: not an array: Q ?".
- **Implicit conversion stops at flat arrays.** These are rejected:
  - `(1,2)=(1,2.0)`
  - `[[1]]=[[1.0]]`
  - `m=["a"->1]; m=["b"->2.5]`
  - `-(1,2.5)`

## 6. Error-message quality
The parser's bracket, string and foreign-keyword diagnostics are good, and carets line up after tabs and UTF-8. The typer's diagnostics are the weak spot.

- ✅ **No column or caret on any typer error.** (a810146; errors raised in Desugar still have no column) In a golf one-liner the line is the whole program.
- ✅ **Errors in imported files are blamed on the `import` line of the main file.** (5a58f0c) This applies to both parse and type errors, through any depth of import.
- ✅ **Missing argument:** (ea237b2) `add a b = a+b; add:3` says `undefined variable '_2'`, a name the user never wrote.
- ◐ **Internal type names leak:** (3a4b9b2, caaba60; an array of unknown element type still prints `?`)
  - `[1,2].pop` gives "cannot convert [int] to BT ?".
  - `.keys` gives `{?:?}`.
  - `sort:3` gives `[?]`.
  - `f -1` on a block says "not defined for fn1".
  - A 10-tuple gives "unknown function 'tuple'".
- ✅ **Overloaded stdlib names report only the last overload** (54147d1) (`max:"hello"` gives "to BT ?").
- ✅ **Misleading hints:** (201dfe5, b9c38f4, 14d7488, ed7c3e5, 67c0d99)
  - A block used where a value is expected gets the irrelevant "to filter, write x[_>2]" hint.
  - `prinln:3` says "not a function: define it with a block".
  - `x := 3` says "'x' is not a function".
  - `a, b = 1` says "cannot index int".
  - `[1,2]-[[1]]` says "put the array on the left" even though the array is already on the left.
  - `c,d,e = (1,2)` says "tuple index must be 0..1", while `a,b = (1,2,3)` silently drops the third value.
- ✅ **Wrong line numbers:** (3247abe, bf6da85, 8c1fd36)
  - A newline inside interpolation code, or a backslash-newline escape, shifts every later line number (also in `--debug`).
  - A lone `\r` is treated as a space by the lexer, but `File.ReadAllLines` splits on it, so the displayed line is wrong.
  - With CRLF the caret is one column too far.
  - `--debug` shows parameters as `0: n'1`.
- ✅ **Hints to add:** (14d7488, 0cad1ab, b9c38f4, 0e8e3fa, 648d576, f0ea083, c47ec37)
  - `else`, which isn't in the foreign-keyword table.
  - `<>` (suggest `!=`).
  - `true` (suggest `T`).
  - `1e5` (no exponent literals).
  - `m[1->2]` (map literals have no `m`).
  - `~ {` (suggest `~{`).
  - `[1;2]`, which says "found end of line" instead of `;`.
  - A form feed or no-break space should be shown escaped in "unexpected character".
- ✅ **Wrong location:** (eef54ae) a missing `}` on a match-arm block is blamed on the outer `?{`.

## 7. Behaviour SPEC doesn't define (decide, then document)
- **String escapes:**
  - Unknown escapes drop the backslash (`\r` becomes `r`, `\0` becomes `0`, `\q` becomes `q`).
  - Only `\n`, `\t` and `\{` exist.
  - Strings inside printed collections are quoted but not escaped: `["a\"b"]` prints `["a"b"]`.
- **Descending ranges:** `5..1` gives `[5,4,3,2]`, so `1..n` with `n=0` gives `[1]`, a trap in loops. But `x@3..1` gives `[]`.
- **Mutating a collection while iterating it:** in-place writes are seen, while reassignment iterates a snapshot.
- **PQ indexing:** `PQ@1` and `PQ@-1` expose the heap's internal order.
- **Scan of an empty collection:** a runtime error; `[]` would be the natural result.
- **Descending sort of strings:** `${-_}` sorts by the reversed string, not in descending order.
- **`sort:xs,{_}`:** a 1-parameter block is treated as a comparator.
- **Anything converts to str in stdlib arguments:** `upper:["a"]` gives `"[\"A\"]"` and `ord:["a"]` gives 91.
- **Float `%` truncates while int `%` floors.**
- **Number parsing:** `float:"0x10"` gives 16.0 but `int:"0x1f"` gives 0. `float:"nan"` and `float:"inf"` are accepted.
- **Float printing:** `1e15` prints as `1e+15`, which can't be read back as an NS literal.
- **SPEC example bug:** `dist x y = {…; d ^^ 0.5}` returns 1 for int arguments, because 0.5 is converted to 0.
- **Input:** CRLF input keeps the `\r` in `read` and `lines`. A NUL byte truncates the line. `read:"."` gives `""`.
- **Output:** write errors are ignored; stdout to `/dev/full` exits 0.
- **Unchecked conversions:** `s = %[1,2]; s += "x"` inserts 0, `xs@"a"` is `xs@0`, and `3.upper` gives `"3"`.

## 8. Missed optimisation
Static-argument hoisting never fires for named-parameter functions. `satInst` expects a raw `_1`, but named parameters are copied into locals. `hits` in `tests/m8_licm.ns` still sorts on every recursion.

## 9. Tooling
`NS_ASAN=1` with `--c` fails to link under the default `cc` (gcc lacks `libasan.so.8`). It works with `CC=clang`.

## 10. Found while fixing
- ✅ **`/tmp` filled up:** `ns f.ns` and `ns test` never deleted their `/tmp/nsXXXX` build directories (9c4225d).
- **`lcm` overflow:** `a / g * b` in `ns_lcm` can overflow (signed undefined behaviour) for large inputs.
- **Runtime panics can't be golden-tested:** the test runner requires exit code 0.
- **`10/-.5`:** divides by zero, because `-.5` is converted to the int 0 (left-operand conversion).

---

## Suggested fix order
1. **§1.1** `own` aliasing. One fix closes about 15 reproducers, including the map-key and memo corruption and the ASan crash.
2. **§1.2 to §1.4** `Opt` effect tracking: the 15969e6 regression, tail-call LICM, and `pop`/`**` writes. Each silently miscompiles normal programs.
3. **§1.5 and §1.6** typer: the recursive return type and the nested-block instance cache.
4. **§3** arity check on calls. It's the most-hit trap.
5. **§2** lexer number crashes. Easy one-liners: ASCII-only digits, `TryParse`.
6. **§1.7 and §1.8** `scombine` in place, and the `sum`/`prod` element types.
