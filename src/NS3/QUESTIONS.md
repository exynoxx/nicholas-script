# NS3 Open Questions

Ranked from most to least important. Each entry gives the issue, the options and a recommendation (**Rec**).
Guiding rule: keep the syntax terse, and make sure the parser never needs more than one token of lookahead or a whitespace rule.
Once a question is resolved, fold the answer back into SPEC.md and delete it here.

---

## 1. The precedence table contradicts the spec's own examples
All three programs in §8 misparse under §3's table:
- `~_%2` is `(~_)%2`, because prefix (level 2) binds tighter than `%` (level 4). The evens example and FizzBuzz both need `~(_%2)`.
- `1..11[~_%2]{^^2}/+` is `1..(11[…]{…}/+)`, because postfix (level 1) grabs `11` before range (level 6) applies.
- `1..n+1/*` is `1..(n+(1/*))` for the same reason. `x@1..3` is `(x@1)..3`, not a slice.

**Part a: where does `~` go?**
- **A.** Lower `~` to sit between compare and logic, like Python's `not`. `~_%2` becomes `~(_%2)` and `~a=b` becomes `~(a=b)`. `-` and `#` stay tight.
- **B.** Keep `~` tight and rewrite the examples as `_%2=0` or `~(_%2)`.

**Resolved: A.** `~` binds below compare.

**Part b: how do postfix ops reach a range?**
- **A.** Make a range a primary: `atom..atom`, where an atom is a primary optionally prefixed by `-` or `#`, with no postfix. Postfix ops then apply to the whole range: `1..10[>2]` is `(1..10)[>2]`. `@` takes a range atom, so `x@1..3` is a slice. A computed end needs parens: `1..(n+1)`.
- **B.** Keep range low, but let the end operand stop before any postfix. Trailing postfix ops apply to the range, so `1..n+1/*` works as written. The catch is that this is non-local: `1..x@0` becomes `(1..x)@0`, and `1..n+1` reads differently from `1..n+1/*`.
- **C.** Keep the table and write `(1..10)[…]` everywhere.

**Resolved: A**, with an **exclusive** end: `1..10` is 1 to 9, `x@1..3` gives 2 elements, factorial is `1..(n+1)/*`.

## 2. `c?a:b` parses as a call
`call = IDENT ":" exprlist` is tried first, so in `c?a:b` the `a:b` part becomes "call `a` with `b`". The new `m@?k` contains op adds more `?`/`:` traffic.

- **A.** Decide by symbol table: `IDENT :` is a call only if IDENT is bound to a block or a built-in. Inference already knows this. `c?a:b` is a ternary and `c?print:1:2` is `c?(print:1):2`.
- **B.** Use a different ternary separator, e.g. `c?a!b`.
- **C.** Require parens: `c?(a):b`.

**Resolved: A.** `IDENT:` is a call only when the name is bound to a block or built-in.

## 3. Blocks capture by value, so loops cannot mutate
`i<9 ~{i=i+1}` never terminates, and `s=0;x{s=s+_}` leaves `s` at 0. The new reference types hide the problem: `c@_=c@_+1` on a captured map works, while the same code on an array (a value type) silently does nothing.

- **A.** Capture by reference everywhere. In v1, blocks may not escape (they cannot be returned or stored), so captured variables stay on the stack.
- **B.** Inline blocks (map, filter, fold, while) use the enclosing scope by reference. Named blocks capture by value.
- **C.** Keep capture by value.

**Resolved: A.** By-reference capture; blocks cannot escape and can recurse.

## 4. The while condition is only a primary
The grammar has `primary "~{" block "}"`, so `i<10 ~{…}` is `i<(10~{…})`. The condition must also be re-evaluated on each pass, which matters for `q ~{…}` on a queue.

- **A.** Make while a statement form, `stmt = expr "~{" block "}"`. The whole expression is the condition and is re-evaluated each pass.
- **B.** Use a block as the condition: `{i<10}~{…}`.

**Resolved: A.** `expr ~{…}` is a statement.

## 5. Collection literal tokens
**Resolved** (spec's collection rules): keep `^^`; `%[`, `Q[`, `PQ[` are single tokens in operand position (`^[`/`^>[` dropped, no max-heap); `->` maximal munch; `[` rule unchanged; `@?` only after an operand.

## 6. Inferring the type of an empty literal
"Inferred from first use" breaks in these cases:
- No typed use at all: `m=[->]; print:#m`.
- First use inside a monomorphised block called with two different types.
- First use on a branch that never runs, or through `+=` on a reference alias.
- Plain `[]` has the same problem.

- **A.** The first use in source order fixes the type, and that includes uses inside blocks and through aliases. A use inside a block called at two different types is a compile error.
- **B.** Default an unconstrained type to `int`.
- **C.** Add type-hint literals such as `[""->0]`.

**Resolved: A + B** (spec): first use in source order, `int` if never used, conflicting block instantiations are a compile error. Still open: whether a later conflicting use converts (Rec below) or errors.

**Rec:** A use that conflicts with the fixed type follows the left-operand rule: the inserted value converts. It is never an error, which keeps the rule consistent with scalars.

## 7. `| & -` as set ops, and `op=` on reference types
`a|b`, `a&b` and `a-b` dispatch on the left operand's type, which fits the left-operand rule. The edges:
- **Precedence.** **Resolved:** single precedence per token; sets need parens.
- **Short-circuit.** **Rec:** `&` and `|` short-circuit only when the left operand is a bool. Sets, maps and ints always evaluate both sides.
- **Element or set on the right.** **Resolved:** the right operand's type decides.
- **`op=` rebinding versus mutating.** **Resolved:** on reference types every `op=` mutates in place.
- **Map `+=`/`-=`.** **Resolved:** `m+=n` merges. Still open: right side wins on conflicts? Non-map right side a compile error?

## 8. Multi-assign from `q.pop`
`d,n = q.pop` needs destructuring, but §6 only defines `a,b = b,a` with matching counts on both sides.

- **A.** When there are N lvalues and exactly one RHS expression of array type, destructure it. Arrays are homogeneous, so every target takes the element type. A length mismatch is a runtime error.
- **B.** Add explicit destructuring syntax.

**Resolved: A**, all RHS evaluated before any assignment; empty pop/peek is a runtime error. Still open: length mismatch as runtime error.

`a,b = q.pop, q.pop` pops twice, left to right. A heterogeneous pair needs to be the same type, so use `[d, str:n]`-style conversion or a v2 tuple.

## 9. Other tokens that play two roles
- **`#` as comment and length.** **Resolved:** `#` followed by a space, tab or end of line starts a comment; otherwise it is length.
- **Leading `-` in a block.** **Resolved:** a leading `-` never starts an operator section.
- **`x/-1`.** **Resolved:** `/op` and `\op` are folds only when no operand follows the op.
- **`=` as assignment and equality.** **Resolved:** statement-start lvalue means assign; write `x[_=n]`. Still open: warn when a filter block's value is an assignment. `m@k=v` inside a block is an assignment, as intended.

## 10. Fold seed and empty fold
A fold seeded with the first element breaks on an empty collection, including an empty set or queue, and cannot seed a different type. `x/0{…}` is division, and `x/{…}=0` is equality.

- **A.** Add a postfix `$seed` after any fold or scan: `x/{$+_}$0`, `x/*$1`. `$` is otherwise unused in postfix position.
- **B.** Put the seed in the block: `x/{0;$+_}`. This is ambiguous with multi-statement blocks.

**Resolved: neither.** No seed syntax; folding an empty collection is a runtime error, op-folds included.

---

## Minor: quick decisions (Rec inline)

- **Integer `/` and `%`.** **Resolved:** floor, like Python. `-7/2` is -4 and `-1%3` is 2, which is golf-friendly for wraparound. Division by zero is a runtime error.
- **Strings as arrays.** **Resolved:** elements are 1-char strings, stored as bytes in v1, with `ord`/`chr` built-ins. Strings never broadcast: `+` concatenates and `*int` repeats.
- **String to number.** **Resolved:** use `strtol`/`atof` semantics. Leading whitespace is skipped and the longest valid prefix is taken, so `"12\n"` gives 12 and `"x"` gives 0.
- **Map `@` operand.** **Resolved:** on maps, `@` takes a plain key. There are no negative indices and no slices, so `m@-1` looks up key -1.
- **Recursion.** **Resolved:** `f={…}` binds `f` before its body is analysed. Still open: return type from the non-recursive branch.
- **Zero-arg calls.** **Resolved for built-ins** (`int:read`). Still open: **Rec:** a bare name bound to a zero-parameter block is called too.
- **Early return.** **Rec:** none in v1.
- **Reassigning with another type.** **Resolved:** between scalars it converts. Still open, **Rec:** Collection against a scalar is a compile error.
- **Ternary branch types.** **Rec:** the else branch converts to the then-branch's type.
- **Broadcasting unequal lengths.** **Rec:** zip to the shorter length.
- **Array concat.** `[]+[]` broadcasts. **Rec:** add `x++y` at add level, which is free because there is no unary `+`.
- **`& |` on ints and bools.** **Rec:** bitwise on ints, logical and short-circuit on bools.
- **Nested `_`, `@` and `$`.** **Resolved:** the innermost block wins.
- **`print` format.** **Rec:** arrays as `[1,2,3]` and maps as `[k->v]`, with strings quoted inside collections. Floats always keep a `.`, bools print as `T`/`F`, and every print adds a newline.
- **Lexer.** **Resolved:** `1..5` lexes as `1` `..` `5`.
- **Grammar bug.** **Resolved:** `lvalue = IDENT { "@" prefix }`, so `x@1..2=…` assigns a slice and `m@k=v` sets a key.
