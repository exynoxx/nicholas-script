# Bugs found (EC 2026 quest 7, 2026-11-07)

Found while writing `AoC/2026/ec/7.ns` part 3. Each repro is a whole `.ns` file, and every output below came from
actually running it.

## 1. `cond ? {stmt}` silently does nothing

```ns
q = Q[1]
t = q{_ < 3 ? {q += _+1}; _}
println: t
```
Output: `[1]`. Expected `[1,2,3]`, or a compile error.

The `{…}` in the branch is a block that is never called, and nothing warns about it. Per SPEC, a block in a ternary
branch is a compile error, but here it gets through. The working form is `(q += _+1; 0)`, see #2.

## 2. `cond ? (x += e)` doesn't parse

```ns
q = Q[1]
t = q{_ < 3 ? (q += _+1); _}
println: t
```
```
2:18: error: expected ')' but found '+='
```
A parenthesised group with two or more statements does work: `(s += _; q += _)` (used in AoC/2025 `grow`) and
`(q += _+1; 0)` → `[1,2,3]`. A single compound assignment inside `( )` should parse too.

## 3. Type inference fails when a recursive call is passed to another function

```ns
step p = (p._1 + 1, p._2)
memo row t = t = 0 ? (0, 0) : (step: row: t-1)
println: row: 3
```
```
1:13: error: cannot index 'p' (?) (when called at line 2 with ?)
```
The base case already fixes the type as `(int, int)`, but `step` gets instantiated with the still-unknown type of
`row: t-1`. I hit this with `row t = (energy, alive, imp)`, a DP table indexed by stage. The workaround is a
self-pushing `Q`: `table = q{… ? (q += step: _; 0); _}`.

## 4. Unclear error messages

**a. Two catch-all clauses**
```ns
g x = 1
g y = 2
```
```
2: error: a function variable cannot be reassigned to another block
```
Should say something like "clause `g y` can never match: `g x` already matches everything".

**b. A local variable with the same name as a user function**
```ns
s a = a
f = { [1,2]{s = _; s} }
println: f
```
```
2:12: error: cannot convert int to a block (when called at line 3)
```
SPEC says a variable shadows builtins and stdlib functions, but a top-level user function apparently can't be
shadowed. Either allow it like builtins, or say "`s` is a function (line 1); pick another name".

**c. Nested mutual recursion**: fixed: functions nested in one block may call ones defined later in it
(`tests/m_nested_mutual.ns`).

## 5. Missing warnings (no error, wrong result)

**a. A clause that can never run**
```ns
h a 0 = 0
h 3 0 = 5
h a b = 9
println: h: 3, 0
```
Output `0`. `h 3 0` comes after `h a 0`, which already matches everything it would, so it can never run. Warn on
clauses that can't be reached.

**b. A pattern name that matches an outer variable binds a new one**
```ns
f x = {
 seed = 0
 g seed 0 = 1
 g i t = 2
 g: 4, 0
}
```
Returns `1`: `seed` in the pattern binds a fresh name and doesn't compare against the outer `seed`. That's
consistent with the design, but it's an easy trap. Warn when a pattern name shadows a variable in scope.
