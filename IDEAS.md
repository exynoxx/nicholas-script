# NS ideas

Language ideas for NS, with status. The `fix` design further down is the main proposal.

## Status

| idea | status |
|---|---|
| `x'` previous-iteration value | done (`f5aed31`) |
| Draining map over `Q`/`PQ` (`@` = priority) | done (`477c161`) |
| `nb4` / `nb8` tuple neighbours | in progress |
| `ns explain` ungolfer | in progress |
| `fix` | designed, see below |
| Assignment in a statement ternary; comparison as a filter's predicate | **done** as `:=` (`c ? x := 1`) + filter predicate compares |
| everything else | proposal |

## Catalogue

Stars rate how new the idea is; cost is the rough implementation effort.

| # | idea | sketch | ★ | cost |
|---|---|---|---|---|
| 0 | Input typed by example | `in` gets its type from the `.in` sample (count header, grid, tuples, groups); inline sample `in"3\n1 2"`; parser fuses into the consuming loop | ★★★ | medium |
| 1 | `ns golf` | the compiler searches for shorter programs that are equal by type and pass the golden tests | ★★★ | high |
| 2 | Checked `# =>` comments | `ns test` compares each `# =>` comment to the line printed; makes the README tour test itself | ★ | tiny |
| 3 | `f ^^@ n, x` | apply `f` n times with automatic cycle detection; without `n`, run to the fixpoint | ★★ | low |
| 4 | Automatic parallel folds | pure fused loop + associative fold op → split across threads | ★★ | medium |
| 5 | `??` search | `a,b,c ?? 1..1000: cond` → nested loops, each condition moved to the outermost loop where it can run | ★★ | medium |
| 6 | `~` invertible blocks | `dec = enc~` for blocks built from invertible primitives | ★★ | medium |
| 7 | Integer width from range analysis | i64 where proven safe, bigint otherwise, no runtime overflow checks | ★★ | high |
| 8 | `x'` | done | ★ | — |
| 9 | Interpolation as a pattern | `?{"move {n} from {a} to {b}" => …}`; each hole's type is inferred from how it is used | ★★★ | medium |
| 10 | Infinite ranges + `@?` | `1..[isprime:_]@0`, `(1..{_*2})@?(>1e9)`; fused, exits early | ★ | low |
| 11 | Distributions `D[…]` | exact probability arithmetic, lifted like broadcasting | ★★ | medium |
| 12 | The binary is the answer | input-free pure program → runs at compile time, binary just prints | ★★ | low |
| 13 | `ns explain` | in progress | ★★ | — |
| 14 | `memo` → DP table | bounded int args → flat array, filled bottom-up when the recursion has a clear order | ★★ | medium–high |
| 15 | Grid helpers | reduced to `nb4`/`nb8` on tuples (no grid type) | ★ | — |
| 16 | Goal-directed evaluation | Icon/Verse-style backtracking over ranges, compiled to loops | ★★★ | high |

### Replacing `cond ~{ body }`

The while loop is a statement in an expression language, it only works through mutation, and `ret` is not allowed inside it. Each of its three uses already has a better home:

1. Counting loops → ranges: `0..n{…}`.
2. Worklists → a draining map over `Q`/`PQ` (**done**).
3. Everything else → unfold: `seed ~{step}` gives the final state and `seed \~{step}` every state; `ret: v` stops. With no seed, `~{…; done ? ret}` is the escape hatch. Unfold is a source for `Lower.iter`, so it fuses.

### `x'` in recursive functions

`x'` is `x` in the caller's frame, which matches loops: a tail call is the next iteration. Use: tree recursion without a parent parameter, `size u = 1 + g@u[!=u']{size:_}/+`. Implemented as hidden parameters on self-calls. Calls from outside pass zero. Mutual recursion and `memo` are compile errors. Open question: zero as the root's seed collides with node id 0.

### Assignment in a ternary branch

**Done, differently:** instead of a position-dependent `=`, `lvalue := e` assigns inside any expression (`c ? d@k := v`); `=` in a ternary branch stays a comparison. The filter half shipped as written. The original proposal follows.

Today `c ? d@k = v` parses as `c ? (d@k = v)`, which compares and throws the result away. To actually assign, you have to write `c ? (d@k = v; 0)`. This comes up constantly in worklist bodies, inside `fix` rules, and in guarded updates such as `d@_<0 ? (d@_ = d@u+1; q += _)`.

**Rule:** when a ternary is a **statement**, its value is discarded, so a comparison in a branch is useless there. In that position, a branch of the form `lvalue = e` or `lvalue op= e` is an assignment:

```ns
g@?q & k2<=3 ? d@(q,_,k2) = min:d@(q,_,k2), du+g@q     # was: ? (d@… = …; 0)
x > m ? m = x : n = x                                     # both branches assign
d@_<0 ? (d@_ = d@u+1; q += _)                             # groups keep working
```

- In **expression** position (`y = c ? a = b : 0`), `=` stays equality, so existing code keeps its meaning.
- Lvalues are a name, `x@i`, `x@i@j`, or a tuple pattern. Anything else is still a comparison.
- An else-less statement ternary already exists (`c ? a`), so only the branch's parse changes.
- The existing `x = 0 ? ret: v` caveat is unchanged: an assignment at the start of the line binds first.

**The opposite case: filters.** A filter holds statements, so `[a@_ = b@_]` parses as an *assignment*, not a test. It silently gives the wrong answer:

```ns
a = [1,2,3]; b = [1,5,3]
(0..3)[a@_ = b@_]       # [] , assigns b@_ into a@_ and keeps nothing
(0..3)[(a@_ = b@_)]     # [0,2], the comparison you meant
a[x = 2]                # [1,2,3], assigns to a fresh x; the value 2 is truthy
```

Both cases follow the same principle: **the position decides what `=` means.**
- Where the value is discarded (a statement ternary's branch), `lvalue = e` assigns.
- Where the value is used as a condition (a filter's **last** statement, which is its predicate), `lvalue = e` compares.
- Earlier statements in a filter still assign: `xs[t = _*2; t > 4]` works as today.

An assignment as a filter's predicate is almost never intended. Making `=` compare there removes a silent wrong-answer bug and the parentheses, and it matches how a ternary condition and `?[ ]` arms already read `=`.

**Where:** `Parser.fs`, in the ternary branch parse (`Expr` / `InThen`), when the ternary sits in statement position: try `lvalue (=|op=) expr` before falling back to `Or`. Add a `SAssign` in the branch, or an `EGroup` of one statement. **Cost:** small. **Test:** `m9_tern_assign.ns`, covering map, index, tuple and compound assignment in each branch, plus a check that expression position still compares. The filter side goes in the filter body parse (`f[` holds statements, see Lexer `opener`): if the last statement is `SAssign` with a single lvalue, re-read it as `Bin("=", …)`. Add `m9_filter_eq.ns` (`[a@_ = b@_]`, `[x = 2]` with `x` defined, earlier statements still assigning).

---

# `fix`: fixpoints as a construct

## Two forms

**Whole state.** `fix s: step`. Repeat `s = step(s)` until the value stops changing, then return it. The update may be anything, including shrinking a set. A pass cap `fix n, s: …` is a runtime error when reached.

```ns
println:#grid - #(fix grid: rm:_)        # AoC 2025 d4: peel accessible cells until stable
```

**Rules (worklist).** `d = fix seed{ body }`. The seed is a map or set. The block runs once per key whose value changed (`_` = key, `@` = value), starting with the seed's keys. Inside the block, updates to `d` re-queue the keys they change.

- **Absent keys:** reading a missing key makes the expression absent, and assigning an absent value does nothing. A rule therefore only fires once its inputs exist.
- **`min`/`max`:** a missing argument is skipped, so no sentinel such as `1e9` is needed.
- **Defined meaning:** repeat until nothing changes, which is always correct. The compiler may pick a faster evaluation order when the shape of the code allows.

```ns
# Dijkstra / BFS
g = [->]; [(0,1,4),(0,2,1),(2,1,2),(1,3,1),(2,3,5),(3,4,3)]{ (a,b,w) = _; g@a ++= (b,w); g@b ++= (a,w) }
d = fix [0->0]{ u,du = _,@; g@u{ (v,w) = _; d@v = min:d@v, du+w } }
println:(4 \~{ _<1 ? ret; v = _; (g@v[(u,w) = _; d@u + w = d@v]@0)._1 }).rev.join:" -> "   # path: filter the result

# AoC 2023 d17: the search state is just the key
d = fix [((0,0),(0,1),0)->0]{ (p,dir,k) = _; du = @
  dirs[!= -dir]{ q = p+_; k2 = _=dir ? k+1 : 1
    g@?q & k2<=3 ? (d@(q,_,k2) = min:d@(q,_,k2), du+g@q; 0) } }

# flood fill (set seed)
seen = fix %[s]{ nb4:_,w,h[g@_ != "#"]{ seen += _ } }

# dependency resolution, AoC 2024 d24: a wire waits until its inputs exist
v = fix init{ gates{ (a,op,b,out) = _; v@out = op:v@a, v@b } }

# connected components: each node takes the smallest label in its component
c = fix map:g.keys{(_,_)}{ l = @; g@_{ c@(_._1) = min:c@(_._1), l } }
```

| rule shape | evaluated as |
|---|---|
| `d@k = min:d@k, du + w` | PQ worklist = Dijkstra; FIFO = BFS when every weight is 1 |
| `s += x` on a set | frontier-only flood fill |
| `max` / `\|` / general | FIFO, re-queue when something improves |
| whole-state form | repeat until equal |

After `fix`, the result is an ordinary map. Questions like "which edges lie on a shortest path" or "how many best paths are tied" become filters over it (`d@u + w = d@v`), so no `prev` bookkeeping is needed.

## Evidence: the AoC repo (2021–2025, 71 solved days)

| year | days | clear | borderline | lines replaced |
|---|---|---|---|---|
| 2021 | 14 | 2 | 1 | ~17 |
| 2022 | 15 | 3 | 1 | ~80 |
| 2023 | 18 | 4 | 2 | ~90 |
| 2024 | 19 | 6 | 2 | ~210 |
| 2025 | 5 | 1 | 0 | ~1 |
| total | 71 | 16 (23%) | 6 (≈30% with these) | ~400 |

- **Shortest path dominates:** 8 days and about 60% of the replaced lines, plus a 45-line `MinHeap` written only to support it.
- **It prevents real bugs:** two hand-written Dijkstras are wrong or hacked. 2023 d17 keys on the cell only, and 2024 d16 tracks direction in a side map. With `fix`, the state is simply the key.
- **The whole-state form is needed:** 2025 d4, 2022 d14p1, 2023 d23 and 2021 d11 shrink or overwrite state, so plain monotone rules can't express them.
- **Out of scope:** constraint deduction (no uses found) and DP over a recursive equation (`memo` covers it).

## Implementation plan


The whole construct is built in the **parser and desugarer**. The typer gets one check on the seed's type. `Lower`, `Opt`, `Repr`, both backends and `runtime.c` need no changes: the rule form desugars to the queue-draining loop that already exists.

### Syntax
- `fix` is a context-sensitive keyword. It is only a keyword when an expression follows it (`startsExpr`, Parser.fs), so `fix = 3` still works.
- `fix [cap,] seed: step` is the whole-state form. Inside `step`, `_` is the current state. The step runs to the end of the expression. A bare function name is called on `_` (`fix g: rm`).
- `d = fix [cap,] seed{ body }` is the rule form. `Postfix` already absorbs the trailing block into `EIter(IMap, …)`, so the two forms are told apart by what follows: `:` means whole-state, no `:` means the block holds the rules. There is no ambiguity.
- No lexer change: `]{` already lexes as `m{`.
- AST: `EFix of Expr option * Expr * Expr` and `EFixRule of Expr option * Expr * Stmt list`.
- Known wart: inside a ternary's then-branch, write `c ? (fix s: f) : g`.

### Desugaring the whole-state form
```
fix n, seed: step  ⇒  ( s = seed; go = T; c = 0
                        go ~{ nx = step[_ := s]; c += 1; c > n ? panic:"fix: no fixpoint after n passes"
                              go = nx != s; s = nx }
                        s )
```
- `!=` compares structurally for every type (it calls `ns_eq`).
- The loop goes through `newLoop()` like `SWhile`, so `_'` = the state one pass ago comes for free.

### Desugaring the rule form
`SAssign([EVar d], [EFixRule …])` → `fixRule`. First `d = seed`; after that `d` is updated in place. The body is rewritten and fed to the existing `iter` as a drain over a worklist `w`:

- **`d@K = E`:**
  - `min:d@A, x` (and `max`) becomes `d@?A ? min:d@A, x : x`.
  - Every other `d@X` read in `K` or `E` adds a guard `d@?X`.
  - The emitted code:
    ```
    k = K; guard ? (v = E; (!(d@?k) | v != d@k) ? (d@k = v; PUSH; 0); 0)
    ```
- **`d += X` (set mode):** insert only if the value is new, and push it.
- **Mode from syntax:** `d@k =` means map mode, `d +=` means set mode. Using both, neither, or `d = …` inside the body is a compile error.

| mode | worklist | PUSH | body prefix | `@` |
|---|---|---|---|---|
| map, FIFO | `Q:(keys:d)` + `inq` set | push if not already queued | `inq -= _` | `d@_` |
| map, PQ | `PQ:((items:d){(_@1,_@0)})` | `w += (v,k)` | `@ != d@_ ? ret` (stale) | priority = value |
| set | `Q:(arr:d)` | `w += x` | — | `_` |

**Choosing PQ or FIFO.** Use the PQ when every update has the shape `d@K = min:d@K, e` with the same `K` on both sides. Everything else uses FIFO. There is no runtime weight check: a PQ that skips stale entries and re-pushes on improvement is correct with negative weights too, and finishes exactly when the Bellman-Ford fixpoint does.

The desugared Dijkstra is the same pattern as the hand-written one in `tests/m9_qdrain.ns`.

### Typing
- Typer-only `fixmap`/`fixset` check that the seed is a map or a set. Example message: `fix with d@k = … needs a map seed ([k->v]), found <t>`.
- Everything else follows from existing rules:
  - result type = seed type;
  - `@` gets the value type;
  - absent keys are handled by guard rewriting, so there is no option type.
- Absent-key semantics apply only to `d@X` reads in the key and right-hand side of an assignment to `d`. Any other read of `d` is a normal read, so use `d@?` there.

### Interactions
- **`x'`:** works in both forms.
- **`ret`:** a bare `ret` skips the rest of the current key.
- **Nested `fix`:** each one rewrites only its own name.
- **`Opt.fs`:** safe. `d` is written via `SetIdx`, so LICM won't hoist reads of it.
- **Refcounting:** check with `NS_ASAN=1` on tests with tuple keys.
- **The seed is aliased:** `d = fix m{…}` mutates `m`. Document it, as for queues.

### Errors
- `fix` with a block that isn't assigned to a name.
- The body never updates `d`.
- `d` is updated both as a map and as a set.
- `d = …` inside the body.
- Wrong seed type (raised by the typer).
- Neither `:` nor a block after the seed.
- Hitting the pass cap is a runtime panic.

### Tests
`m9_fix_state`, `m9_fix_peel` (AoC 2025 d4), `m9_fix_cap`, `m9_fix_bfs`, `m9_fix_dijkstra` (including 2023 d17 with state in the key), `m9_fix_negw`, `m9_fix_flood`, `m9_fix_gates` (2024 d24), `m9_fix_absent`, plus the `e_fix_*` error tests. Run them with `--c` too, and run `NS_ASAN=1` on dijkstra, gates and flood.

### Phases
1. **Whole-state form, ~60 lines.** Ast, Parser `BaseAt` case, Desugar `EFix`, SPEC, the vscode keyword, three tests.
2. **Rule form with FIFO and set mode, ~130 lines.** `fixRule` (walker, assignment rewrite, guards, worklist, `@`, cap), typer `fixmap`/`fixset`, tests.
3. **PQ selection, ~25 lines.** Pattern check, PQ seed, stale-entry prefix; rewrite `examples/dijkstra.ns` with `fix`.

### Open questions
1. **Assignment in a then-branch.** `c ? d@k = v` currently parses as a comparison, so it needs `c ? (d@k = v; 0)`. Proposed fix: see "Assignment in a ternary branch" above.
2. **Whole-state form with a step that mutates a map or set in place.** The step returns the same object, so old and new compare equal and the loop stops after one pass. Options: (a) document "return a new value", (b) snapshot the state with a new `ns_map_copy`, (c) panic. Default: (a).
3. **How far absent reads reach.** Only key and right-hand-side reads, or should conditions go quiet too?
4. **Outer names.** Is `u, du = _, @` at the start of the body enough, with no `@@`?
5. **`max` rules get FIFO.** Should a max-heap PQ exist so they get best-first order?
6. **`fix` as a keyword.** Context-sensitive, or fully reserved?
