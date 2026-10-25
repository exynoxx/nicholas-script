# NS Language Spec (draft v1)

A terse, golf-style, statically typed language compiled to LLVM IR and then to a native binary.
Types are never written; every expression has one type known at compile time.

## 1. Lexical

| Token        | Form                                   | Notes |
|--------------|----------------------------------------|-------|
| int          | `42`                                   | 64-bit signed |
| float        | `3.14`, `.5`                           | 64-bit double |
| string       | `"abc"`, `"x={x}"`                     | `{expr}` inside is interpolation, `\{` escapes |
| bool         | `T`, `F`                               | |
| ident        | `[a-zA-Z][a-zA-Z0-9]*`                 | |
| `_` `_1` `_2`| implicit block args                    | |
| `$`          | fold accumulator                       | |
| `@`          | index op / current index inside block  | |
| prev         | `x'`, `_'`, `x''`                      | a name with trailing `'`s (no space): its value 1, 2, … iterations ago (§3 Block shortcuts) |
| comment      | `# ...` to end of line                 | `#` followed by space or end of line; otherwise `#` is length |

### Statement termination
- A **newline** ends a statement.
- `;` ends a statement when several share one line: `a=1; b=2; println:a+b`.
- A newline does **not** end a statement when inside open `(` or `[`, or when the line ends with a binary operator or `,`
  and the next line is indented more than the statement's first line (so a forgotten operand errors on its own line).
  A trailing `..` is an open slice (`s@1..`) and ends the statement outside brackets.
  Likewise a more indented line that starts with a binary operator (`+ - * / = < ? :` …) or a map `{` continues the
  statement: `x = 10\n  - 1` is 9, `ys = xs\n  {_*2}` maps.
- Inside `{ }` and a filter `xs[ ]` the same rules apply; their statements are separated by newline or `;`
  (`xs[s = _@1\n  #s > 1]`). A plain `[1,\n 2]` array literal is like `( )`.

## 2. Types

v1 primitive types: `int` (i64), `float` (f64), `string`, `bool`.
Composite: arrays `[T]` (homogeneous), functions (blocks).
Deferred to v2: `uint`, `int8..int64`, explicit width ints.

### Inference
Names start with a letter and may contain letters, digits and `_` (`num_items`); a leading `_` is reserved for the implicit `_ _1 _2 …`.
Each variable's type is fixed by its first assignment. Re-assigning a value of another type converts it to the variable's type (implicit conversion, below).

### Implicit conversion — the left operand rules
For any binary operator, **the left operand's type is the result type**; the right operand is converted to it.
This is the last of three steps; see the operator matrix in §3 for the overloads and elementwise rules checked first.

```
"1"+1     # "11"   (int -> string, then concat)
1+"1"     # 2      (string -> int, then add)
1+2.7     # 3      (float -> int, truncates)
1.0+2     # 3.0
"a"**3    # "aaa"  (** repeats; "a"*3 is a compile error)
T+1       # T      (bool or)
```

Integer `/` and `%` are **floor** division/modulo (`-7/2 = -4`, `-7%2 = 1`). Division by zero is a runtime error.

Comparisons (`= != < > <= >=`) convert the right side to the left type and return `bool`.

### Conversion table (right side → left type)

| from \ to | int                 | float        | string        | bool        |
|-----------|---------------------|--------------|---------------|-------------|
| int       | —                   | exact        | decimal text  | `≠0`        |
| float     | truncate            | —            | shortest text | `≠0.0`      |
| string    | `strtol`-style: skip whitespace, longest valid prefix, else `0` | same, `0.0` | — | non-empty |
| bool      | `1`/`0`             | `1.0`/`0.0`  | `"T"`/`"F"`   | —           |

### Explicit casts
Casts are ordinary calls to built-in functions named after the type:
```
int:"42"      float:x     str:3.5     bool:n
```

## 3. Expressions

### Precedence (high → low)

| Level | Ops | Assoc |
|-------|-----|-------|
| 0 primary | `a..b` range (start is an atom; end is `+ - * ** / %` over atoms, so `0..n-1` ends at n-1; postfix `{}` `[]` `/+` still apply to the range) | none |
| 1 postfix | `x{…}` map, `x[…]` filter, `x${…}` sort, `x/{…}` `x/op` fold, `x\{…}` `x\op` scan, `x@i` index, `.name` method call; `x'` previous iteration (part of the name) | left |
| 2 prefix  | `-x` `#x` (length) | right |
| 3 power   | `^^` | right |
| 4 mul     | `* ** / %` | left |
| 5 add     | `+ - ++` | left |
| 5.5 shift | `<< >>` (ints; `>>` is arithmetic) | left |
| 7 compare | `= != < > <= >=` | none |
| 7.5 not   | `!x` (binds looser than compare, like Python `not`: `!_%2` = `!(_%2)`); `!f` of a function or block is the negated function: `g = !ws`, `xs.sortby:!isupper`, `!{_>2}` | right |
| 8 logic   | `&` then `^` (xor) then `\|` (short-circuit on bools; bitwise on ints; set ops on sets) | left |
| 8.5 map default | `m ?? d` (m with default value d for missing keys, see maps) | left |
| 9 ternary | `x ?{…}` `x ?[…]` match (§5); `c ? a : b`; `c ? a` without `:` runs `a` only when `c` holds (value 0); branches of unrelated types run as a statement (value 0) | right |
| 10 call   | `f:a,b` (args extend to end of statement / closing bracket, so `(f:1,2, f:3,4)` is an error: write `((f:1,2), f:3,4)`; passing a block more arguments than it has parameters / highest `_k` is an error). `IDENT:` is a call only if the name is bound to a block or built-in; otherwise `:` belongs to a ternary. A block can be passed to a function and called through its parameter (`apply f x = f: x`, `{_1: _2}`), except in a ternary's then-branch, where `p :` is the ternary's `:` (write `c ? (f: x) : y`) | right |
| 11 assign | `x = e`, `a,b = b,a`; `x := e` inside an expression | right |

`=` in expression position is equality; at statement start with an lvalue on the left it is assignment. A statement `a = b` whose left side isn't an lvalue (`3 = x`, `(x) = 3`, `T = 5`) is an error unless it is a block's last statement (its value).
A filter's last statement is its predicate, so there `=` compares too: `(0..3)[a@_ = b@_]` keeps the indices where
`a` and `b` agree (earlier statements still assign: `xs[t = _*2; t > 4]`).

`lvalue := e` assigns anywhere an expression goes, and its value is the new value: `c ? d@k := v`,
`x > m ? m := x : n := x`, `y = (z := 4) + 1`. In a ternary branch, `=` compares; use `:=` to assign.

### Operator matrix
A binary operator is resolved at compile time in three steps; the first that applies wins:

1. **Structural overload**: an exact (left type, operator, right type) entry in the table below.
2. **Elementwise**: arrays and tuples with arithmetic (`+ - * / % ^^`, and `^ << >> >>>` on arrays) apply the
   operator per element, against a scalar or a same-shaped right operand (see Broadcasting).
3. **Left-operand conversion**: the right operand is converted to the left type (§2).

| left | op | right | result |
|---|---|---|---|
| array | `**` | int | the array repeated n times: `[2,3]**2` → `[2,3,2,3]`; `[x]**n` is n copies of x, nested arrays deep-copied |
| str | `**` | int | the string repeated: `"ab"**3` → `"ababab"` |
| array, str | `**` | bool | `T` keeps it, `F` gives empty (bool converts to 1/0): `"s"**(n>1)` |
| set, map, queue, PQ | `**` | int, bool | 0 or `F` empties it in place (aliases see it); anything else leaves it unchanged |
| int, float | `**` | any | compile error (power is `^^`) |
| str | `*` | any | compile error (use `**`) |
| array, tuple | `+ - * / % ^^` | scalar | elementwise: `[1,2]*2` → `[2,4]`, `(1,2)*3` → `(3,6)` |
| array, tuple | `+ - * / % ^^` | same shape | pairwise: `[1,2]+[10,20]` → `[11,22]` |
| int, float | arithmetic | array | compile error (put the array on the left) |
| str | `/` | str | split: `"a,b"/","` → `["a","b"]` |
| str | `/` | other non-block | compile error |
| str, collection | `/{…}` `/op` | block or op | fold (unchanged) |
| int | `{…}` `[…]` `/` `\` `${…}` | block or op | runs over `0..n`: `5{_*_}` → `[0,1,4,9,16]`, `5/+` → `10` |
| array | `++` | array / set / Q / PQ / element | concat / append |
| set, Q, PQ | `++` | array / set / Q / PQ / element | add every element / add one |
| set | `& \| + -` | set | intersect, union, union, difference |
| set, queue, PQ | `+` `-` | element | add / remove |
| map | `+` / `-` | map / key | merge / delete key |
| — | unary `-` | str | reversed: `-"abc"` → `"cba"` |
| any | `= != < > <= >=` | any | right converted to left type; bool (no broadcasting) |

### Arrays and ranges
```
x = [1,2,3]
r = 1..10        # range, end exclusive: 1..9
x@0              # index; negative counts from end: x@-1; out of range is a runtime error
x.one            # first element, same as x@0 (also one:x)
x@1..3           # slice (elements 1,2); `@` takes a range operand
x@..2  x@1..     # open slices: first 2 / from 1 to the end; x@-2.. is the last 2
0..xs            # an array as a range end means its length: 0..#xs (also in slices)
#x               # length
```

### Collection operators

| Op       | Syntax          | Example      | Meaning |
|----------|-----------------|--------------|---------|
| map      | `x{body}`       | `x{_^^2}`    | new array of body results |
| filter   | `x[body]`       | `x[_>2]`     | keep items where body is truthy |
| fold     | `x/{body}`      | `x/{$+_}`    | `$` = accumulator, seeded with first element; no seed syntax; folding an empty collection is a runtime error, except `$+_ $*_ $&_ $\|_ $^_` (and `/+ /* /& /\| /^`), which give the identity: `0`/`0.0`/`""`/`[]`, `1`, `T` (int `-1`), `F`/`0` |
| op-fold  | `x/op`          | `x/+`        | sum, `x/*` product, `x/>` max, `x/<` min, `x/&` all, `x/\|` any |
| scan     | `x\{body}` `x\op` | `x\+`      | running fold, returns array |
| sort     | `x${key}`       | `w${#_}`     | array sorted by key (stable; `x${-_}` descending); later ops see the sorted order: `w${#_}{upper}`. On a map `_` is a `(key, value)` tuple: `m${-_2}{_1}` = keys by value, largest first |

`/op` is always a fold (`xs/+ * 2` doubles the sum), except `/-` followed by an operand, which is division by a negation (`x/-1`, `x/-#y`, `x/-.5`).
Chained maps and filters (and a fold or scan at the end) fuse into one loop with no intermediate arrays, so each
element passes through every stage before the next element starts: side effects interleave per element.
`c = 0; [1,2,3]{c += 1; c}{_ * c}` is `[1,4,9]`, not `[3,6,9]`.
`x.take:n` keeps the first n elements (`x.take:-n` the last n; clamped to the length; strings give strings). On a map/filter
chain (`xs[>5]{*2}.take:2`) the fused loop stops as soon as it has n results, so an expensive or huge source is not run to the end.
Indexing a chain does the same: `chain@k` (k a literal >= 0 or a variable) stops after k+1 results, and `chain.one`
(= `chain@0`) after the first: `1..1000000[_*_ > 50].one` is 8 after 8 elements. Too few results is the usual
index-out-of-range error.
`${` directly after an expression is a sort; elsewhere `$` stays the fold accumulator.
Disambiguation: `[` after an expression (spaces allowed: `xs [>0]`) is a filter; otherwise it starts a literal. `/` followed by `{` (spaces allowed: `x / {$+_}`) or an operator token is a fold; otherwise division.

### Block shortcuts
- **Operator section**: a block body starting with a binary operator gets `_` on its left: `x{*2}`, `x[>2]`, `x{^^2}`. `-` never starts a section (`x{-1}` is the constant -1).
- **Function section**: a map, filter or plain block whose whole body is a function name calls it on `_`: `x{int}` is `x{int:_}`, `x[even]`, `sortby:xs,{str}`; so does a negated one: `s[!ws]` is `s[!ws:_]`, and a negated block: `x[!{_>2}]` calls `!{_>2}` on each element
  (the block must return bool). A plain block as the whole body is an error: write `x[_>2]`, not `x[{_>2}]`.
  Blocks are not values in collections or conditions: `x{{_+1}}`, `[{_}]` and `f ? a : b` with a block `f` are compile errors.
- Nested blocks: the innermost block owns `_`, `@`, `$`.
- **Previous iteration** `x'`: the value of variable `x` one iteration ago in the innermost enclosing loop (a
  map/filter/fold/scan/each block, or a `~{` while loop); `x''` two ago, and so on. Works for `_`, `@`, `_1`… and
  user variables. A user variable's `x'` is its value at the end of the previous iteration; before enough
  iterations it is the type's zero (`0`, `0.0`, `""`, `F`, empty collection), also when `x` was assigned before
  the loop: the outer value never seeds it.
  `[3,5,9,10]{_ - _'}` is `[3,2,4,1]`; `s = 0; 1..5{s += _; println:s - s'}` prints `1 2 3 4`.
  A filter's `_'` is the previous source element (kept or not); in a chain (`xs[>4]{_ - _'}`) each block sees its
  own input. A fold's first element seeds `$` without running the block, but still becomes `_'`. The innermost
  loop owns `x'` like `_`: in `xs{r = _; r{_ + _'}}` the inner `_'` restarts at 0 for every row. A plain block
  (`f = {_'}`) or a `?[ ]` arm is not a loop: `x'` there is a compile error, as is `'` after anything but a
  name (`(a+b)'`, `5'`) and assigning to `x'`. The state is a carried variable, so chains still fuse into one loop.
- `_` current element, `@` (bare) current index, `$` fold accumulator. Over a PQ of tuples, `@` is the priority and `_` the value.
- `_1`, `_2`, … for multi-argument blocks. Inside a map/filter/fold/scan block they destructure the element instead (`_1` = `_@0`): `xs.pairwise{_2-_1}`, `(zip:a,b){_1*_2}`.
- Last expression in a block is its value.

### Array concat / append
`a++b` concatenates two arrays (`b` may also be a set, Q or PQ); `a++x` appends an element (converted to the element type). On a set, Q or PQ, `s++xs` adds every element of a collection `xs` and `s++x` adds one, so `q ++= [a,b]` enqueues both. Arrays compare lexicographically with `= != < > <= >=` and return bool (comparisons do not broadcast).

### Broadcasting
Arithmetic between an array and a scalar, or two arrays of equal length, applies elementwise:
`x*2`, `x+y`, `x^^2`. Left-operand conversion rules apply per element. `*` never repeats: `[0]*4` is `[0]`; use `[0]**4`. Unequal lengths are a runtime error; `scalar op array` is a compile error.

### Strings
Strings are byte strings. They behave as arrays of 1-character strings for `{}`, `[]`, `/`, `\`, `@`, `#` only.
Strings never broadcast: `+` concatenates, `**int` repeats, `/str` splits (`"a b"/" "` → `["a","b"]`). `ord:"a"` → 97, `chr:97` → `"a"`.

`nums:s` returns every integer in a string (`-` is a sign unless it follows a letter or digit): `nums:"1-3 x=-4"` → `[1,3,-4]`.
`"hello"{_*2}` → `["hh","ee","ll","ll","oo"]`.

### Hash maps, hash sets, queues, priority queues

| Type        | Literal                 | Empty    | Notation in docs |
|-------------|-------------------------|----------|------------------|
| hash map    | `["a"->1, "b"->2]`      | `[->]`   | `{K:V}` |
| hash set    | `%[1,2,3]`              | `%[]`    | `%T` |
| FIFO queue (linked list) | `Q[1,2,3]` | `Q[]` | `Q T` |
| priority queue (min-heap) | `PQ[3,1,2]` | `PQ[]` | `PQ T` |

`%[` is only a literal in prefix position; in binary position `%` keeps its arithmetic meaning. `Q` and `PQ` are reserved words (like `T`/`F`) and cannot be variable names.
For a max-heap, push negated priorities: `q+=[-p,v]`.
The element type of an empty literal is inferred from its first use (`m@"x" = 1` fixes `[->]` to `{string:int}`).
Keys/elements may be any hashable value: `int float string bool` and arrays of those.
Arrays compare lexicographically, so a PQ of `[priority, value]` arrays orders by priority: `q+=[d,n]`.

### Sorted trees

`BT[3,1,2]` (empty `BT[]`, notation `BT T`) is a sorted set: a balanced binary search tree (treap). `BT:xs` converts a
collection; `BT:xs,{key}` orders by the key block instead (`BT:words,{#_}` by length); elements with equal keys are
ordered by their own value, and an element is stored once. The key is computed when an element is inserted, removed or looked up.
A reference type, like sets.

| Op | Meaning |
|----|---------|
| `t+=x`, `t-=x` | insert / remove, in place |
| `t@?x` | contains |
| `t.min`, `t.max` | smallest / largest element (by key) |
| `t.pop` | removes and returns the smallest |
| `#t`, `arr:t` | size; elements in order |
| `t{…}` `t[…]` `t/…` | in order; map and filter return arrays |

Maps, sets and queues are **reference types** (mutated in place, shared on assignment). Arrays and strings remain values.

| Op                 | Map `m`             | Set `s`           | Queue / PQ `q`               |
|--------------------|---------------------|-------------------|------------------------------|
| get / peek         | `m@k` (error if missing, unless `m ?? d`) | —       | `q@0` front/top without removing (Q also: `q@-1` back) |
| set / insert       | `m@k = v`           | `s+=x`            | `q+=x` (Q: push back; PQ: insert by priority) |
| remove             | `m-=k`              | `s-=x`            | `q.pop` returns and removes front/top |
| contains           | `m@?k`              | `s@?x`            | `q@?x` (linear scan)         |
| size               | `#m`                | `#s`              | `#q`                         |
| map / filter / fold| `m{…}` `m[…]` `m/…` | `s{…}` `s[…]` `s/…` | `q{…}` etc. **drain** `q` (see below) |
| union / inter / diff | —                 | `a\|b` `a&b` `a-b` | —                           |
| keys / values      | `m.keys` `m.vals`   | —                 | —                            |

In map blocks `_` is the value and bare `@` is the key (same as element/index for arrays). In `m{…}` a top-level `_ = e` writes the value back into m: `m{_ = _ + 1}` updates m in place (filters and arrays still compare, and so do `m{(_ = e)}` and the section `m{=e}`).
A block over a queue **drains** it: `q{…}` `q[…]` `q/…` `q\…` pop until `q` is empty (Q: front to back; PQ: priority order),
and the body may push onto `q` (`q += x`, `q ++= xs`): pushed items are visited by the same loop, so `q{…}` is a
worklist loop. Map, filter and scan return an array of results in pop order. On a Q, `_` is the popped element and `@`
counts (0, 1, 2, …). On a PQ of tuples, `@` is the priority (first value) and `_` the value, or the rest as a tuple for
wider tuples: `PQ[(p,a,b)]{…}` has `@` = p, `_` = `(a,b)`. On a PQ of anything else (ints, arrays) `@` and `_` are both
the element. Queues are references, so aliases see the drained queue. `ret` (bare, no value) skips the rest of the
body for this element and the loop goes on popping: `q{ @ > d@_ ? ret; … }` skips stale Dijkstra entries. To iterate
without draining, iterate a snapshot: `(arr:q){…}`. (`s ++= q` and `arr:q` don't drain.)
`m{…}` returns a map with the same keys; `s{…}` returns a set; `m[…]`/`s[…]` return the same kind.
A set chain that ends in a fold or scan is lazy, like a LINQ enumerable: `s{f}/+` folds every mapped value and builds no set, so equal values are not merged (`%[1,2,3]{_%2}/+` → 2).
`@?` (contains) also works on arrays and strings: `"hello"@?"ll"`.
Conversions: `map:pairs`, `set:arr`, `Q:arr`, `PQ:arr`, `arr:s` (to array; map → array of keys).
Truthiness: an empty map/set/queue is falsy.

```
c = [->] ?? 0; "hello"{c@_+=1}          # char counts
s = %[]; [3,1,3]{s+=_}; println:#s         # 2
q = PQ[(0,src)]; q{ @ > d@_ ? ret; g@_{ ...; q += (nd,v) } }   # Dijkstra skeleton
```

Lexing/typing rules for collections:
- `%[`, `Q[`, `PQ[` are single tokens, only in operand position. `->` uses maximal munch. `@?` is one token only after an operand (`x{@?1:0}` is bare `@` then ternary).
- Empty literals: the first use in source order fixes the element type. A literal that is never used with a type defaults to int. A use inside a block that is called at two different types is a compile error.
- `| & -` keep their single precedence level, so mixed set expressions need parens. On sets, the right operand's type decides the meaning: a set means union/inter/diff, an element means add/remove.
- `@` on a map takes a plain key: no negative indexes and no slices.
- Maps iterate and print in hash order. Reading a missing key is a runtime error unless the map has a default value:
  `m ?? d` sets m's default to d in place and returns m (`c = [->] ?? 0`, `g = [->] ?? []`). From then on every read of
  a missing key, including the read in `m@k op= x`, gives d (no insert), so `c@_+=1` counts. `??` on a non-map is a
  compile error; it binds looser than `|`, so `(m ?? 0)@k`. Maps built from m (`m{…}`, `m[…]`) don't inherit the default.
- Popping or peeking an empty queue is a runtime error.
- Destructuring: N lvalues with one array on the right unpack it (`d,n = q.pop`). All right-hand expressions are evaluated before any assignment.

### Compound assignment
`x op= e` for every binary op (`+= -= *= **= /= %= ^^= &= |=`), meaning `x = x op e`, except that on maps/sets/queues every `op=` mutates in place, so aliases see the change. `m+=n` merges map `n` into `m`.
`m@k ??= v` and `m ??= (k, v)` insert v only when k is missing; v is not evaluated when k is present (`g ??= (u, [])`).

## 4. Functions
```
sq = {_^^2}
add = {_1+_2}
add:3,4            # call
x.sq               # method call: sq:x
x[>2]{*2}.sort.println
add a b = a+b      # named parameters: same as add = {_1+_2}
dist x y = {       # a { } body holds statements
  d = x*x + y*y
  d ^^ 0.5
}
```
Named parameters are local to the function: assigning one never changes an outer variable of the same name.
A parameter `(a, b, …)` unpacks a tuple or array argument (`_` skips an element: `mid (_, b, _) = b`): `dot (a, b) (c, d) = a*c + b*d`, `dot:(1,2),(3,4)`.
Blocks capture variables from the enclosing scope **by reference** (`s=0; x{s+=_}` updates `s`). In v1 blocks may not escape (be returned or stored in collections), so captures live on the stack. A block isn't a value: it can be called, named, or passed to a function that takes a block (`sortby`, `memo`, `BT`), but printing one, putting it in a tuple or a ternary branch, or returning one is a compile error.
A name is bound before its block body is checked, so blocks can recurse: `f={_<2?_:f:_-1}`.
Top-level functions (`f a = …`, `f = {…}`, `f = memo:…`) may call functions defined later in the file, so they can be
mutually recursive: `ev x = x=0 ? T : (od:x-1)` before `od x = x=0 ? F : (ev:x-1)`. Top-level code still calls a
function only after its definition.
`a = f` with `f` a function makes `a` an alias: `a:x`, `x.a` and `xs{a}` call `f` (`fa = fact` then `fa:5`). An alias
cannot later be reassigned to another function. Aliasing a stdlib function works the same (`m = max`, `u = upper`), and the alias keeps
calling it after the name is shadowed by a variable (`max = 3` then `m:xs`).
A variable or parameter named like a builtin or stdlib function shadows it in its scope: after `max = 3`, `xs{max}` is `3` per element and `max:xs` is an error ('max' is a variable here); `.max` method calls still reach the function.
A function's parameter types are inferred per call site (monomorphised at compile time).

`memo:{…}` memoizes a block (Python's `@lru_cache`, unbounded): each call first looks its arguments up in a cache,
keyed on the argument (or the tuple of arguments), and runs the body only on a miss. Each typed instance has its own cache.
Captured variables are not part of the key.
```
fib = memo:{_<2 ? _ : (fib:_-1)+(fib:_-2)}
fib:90                                       # 2880067194370816120, instantly
paths = memo:{_1=0 | _2=0 ? 1 : (paths:_1-1,_2)+(paths:_1,_2-1)}
memo paths r c = r=0 | c=0 ? 1 : (paths:r-1,c)+(paths:r,c-1)   # same, with named parameters
```
`memo f a b = body` is `f a b = body` memoized (`f = memo:{…}` with named parameters).

`lazy f a = body` (or `lazy f = {…}`, top-level only) marks a function whose array result streams into its consumer
instead of being built first (a C# iterator without `yield`). When a call is the source of a chain (maps and filters
pass through) ending in `@k` (k ≥ 0), `.one`, `.take:n`, `.any`, `.all`, `/|` or `/&` of bools, or `@?x`, the body
hands its elements over one at a time and stops once the ending has its answer. Any other use gets the plain array.
Removing `lazy` changes no result, only the cost (and how side effects interleave). Statements before the body's last
expression run once per call; the last expression streams: a fused chain, `a ++ b` (b only if more are wanted),
`c ? a : b`, `[x, y]`, `.flatten` of a map (a flat-map), a call to a lazy function (recursion works), or any other
array, element by element.
```
lazy pairs xs = 0..xs{i = _; ((i+1)..xs){(xs@i, xs@_)}}.flatten
(pairs:nums)[_1 + _2 = 10].one      # stops at the first match: never builds the n² pairs
lazy walk i = i < #t ? (walk:2*i+1) ++ [t@i] ++ walk:2*i+2 : []
(walk:0).take:3                     # 3 smallest of a heap-ordered search tree; the rest is not visited
```

## 5. Control flow
```
c ? a : b                  # ternary
c ? a                      # if without else
c ? (x = 1; y = 2) : 0     # statement group: `;` inside parens, value = last statement
1..10{println:_}             # for-each = map
cond ~{ body }             # while statement: whole `cond` expression re-evaluated each pass
```

`ret: v` ends the enclosing block (function body, map body, group) with value `v` (`ret: a, b` = the tuple `(a, b)`).
Bare `ret` (no value) ends it with 0, so `cond ? ret` skips the rest of the block: in a map body, of this element only.
As a guard, `cond ? ret: v` returns `v` when `cond` holds and otherwise runs the rest of the block, so an early exit
needs no nesting; a recursive call after it is still a tail call. `ret` works only as a statement of a block, not
inside an expression or a `~{ }` loop. Mind `x = 0 ? ret: v` at the start of a line: that assigns; write `(x = 0) ? ret: v`.
```
f q seen = {
    #q < 1 ? ret: #seen
    (u, i) = q.pop
    i > 10 ? ret: #seen
    seen @? u ? ret: f: q, seen
    seen += u
    f: q ++ next@u {(_, i+1)}, seen
}
```

### Match `?{ }` / `?[ ]`

```
n ?{                       # with a subject: arm values are compared with =
  1 => "one"
  2, 3 => "few"            # any of several values
  >9 => "big"              # an arm starting with a comparison tests the subject
  _ => "some"              # default arm (also: a last arm without =>)
}
?{ x<0 => "neg"; x=0 => "zero"; "pos" }      # no subject: each arm is a condition
[1,5,12] ?{ >10 => _*100; _ }               # array subject: the match maps over the elements
[1,2,5,9] ?[ 1, 2; >8 ]                     # ?[ ] keeps the elements matching an arm → [1,2,9]
l ?{ [1,2,3] => "exact"; [] => "empty"; _ => "other" }   # an array-literal pattern: l is matched whole
n ?{
  1 => "one"
  2, 3 => {                # a block arm: several statements, the last expression is the value
    c += 1
    "few"
  }
  _ => { c = 0; "some" }
}
```
Arms are separated by newline or `;` and tried in order; the first match wins. Inside a subject match `_` is the
subject (the element, for an array). If any pattern is an array literal (`[1,2] =>`, `[] =>`, `<[2] =>`), an array
subject is compared as a whole value instead of mapped over. An arm's result may be a statement (`5 => c += 1`, value 0), or a block `=> { … }` of statements separated by
newlines or `;`: it runs in the match's own scope (`_` is still the subject, assignments are visible after the match),
and its value is its last expression, or 0 if that is a statement. An arm block is not a function value; as in any block,
a body starting with an operator gets `_` on its left (`=> { *2 }` is `_*2`). Without a default
the match behaves like `c ? a`: arms run for effect and the value is 0. In `?[ ]` an arm without `=>` keeps its
elements, `=> F` drops them, and `_ =>` decides the rest (default `F`). Every `?[ ]` arm must give a value, which is
tested for truthiness on its own (`?[>4 => "a"; _ => 0]` keeps the elements over 4); a statement arm, or an arm block
that doesn't end in a value, is an error. `?[` directly after an operand is a match filter unless its `]` is followed
by `:` (then it is a ternary with an array literal: `c?[1]:[2]`), except when the brackets can't be an array literal
(`?[>4]`, arms with `=>` or `;`) or an earlier `?` is still waiting for its `:` (`c ? xs?[>4] : xs`).

## 6. Statements
`import "file.ns"` splices in another file's statements (path relative to the importing file; each file is included once, so imports may repeat or be mutual).

```
x = expr
a,b = b,a                  # parallel assignment
expr                       # expression statement
?? stmt                    # run stmt; if it fails at runtime, skip the rest of it silently
```

`?? stmt` catches every runtime error raised while stmt runs (missing map key, index out of range, popping an empty
queue, division by zero, ...), including inside functions it calls: `?? println:m@a + m@b` prints only if both keys
exist, `?? out@k = m@a + m@b` assigns only if every read succeeds. Effects that happened before the failure stay
(`?? xs{println:m@_}` prints up to the first missing key), and so does anything the failing part allocated (it leaks).

Debug mode: `--debug` (on run/build/test) prints `line: name = value` after every assignment to a user variable, including `x@i = v` and compound assignments.

Explain: `ns explain f.ns` prints the program as Python-flavoured pseudocode: maps, filters and folds become `for` loops or
comprehensions, blocks assigned to names become `def`s, implicit names (`_ @ $`) get readable names, and the first assignment
of a collection variable carries its inferred type as a comment (`xs = [3, 1, 4]   # list[int]`). Coloured on a terminal
(`NO_COLOR` turns that off). It is a reading aid, not Python: `a ..< b` is a range and `//` is floor division.

## 7. Built-ins and standard library
Language primitives (in the compiler): `print` (no newline), `println`, casts `int float str bool`, `#` length, `@?` contains, ranges and slices,
collection literals and `set: Q: PQ: arr:`, `pop`, `keys`, `vals`, tuples, `[x]**n`.
Printing and `str` show a string bare, but strings nested in arrays, maps and tuples are quoted: `println:["a"]` → `["a"]`. `join` does not quote; `xs.join:sep` puts sep between elements, `xs.join` just concatenates.

Tuples: `(a,b)`, `(a,b,c)` … up to 9 elements of any types; immutable; `t@0` (literal index), `t._1` (1-based, = `t@0`), `a,b = t` or `(a,b) = t` (one name per element); `+ - * / % ^^` work elementwise (`(1,2)+(10,20)` → `(11,22)`, `(1,2)*3` → `(3,6)`); `tuple2:xs` `tuple3:xs` take the first 2 or 3 array elements; compare lexicographically; usable as map keys, set and PQ elements.
Repeat: `xs**n` repeats an array, `s**n` a string; `[x]**n` builds n copies of x, nested arrays deep-copied.

Everything else is the **standard library, implemented in `runtime.c`**. Each function is declared there by a signature line
```
//@ name (param, ...) result [= c_symbol]
```
using `int float str bool`, type variables (`T`, `K`, …), `[t]`, `(a,b)` and blocks `fn(t)->r`. Overloads are tried in order
(an exact type match wins, otherwise the first one with conversions). Adding a function = writing it in C with its `//@` line;
the compiler needs no change. A block argument is passed to C as a callback, e.g. `bsearch:xs,{>=5}`, `sortby:ws,{#_}`; a block with several parameters is `fn(A,B)->r` (C passes `x` as an array of pointers to the arguments).

Current library (see `grep '^//@' runtime.c`):
- I/O: `read` (a line, `""` at EOF), `lines` (all remaining stdin lines); zero-argument functions are called by bare name. With a path: `read:"f.txt"` (whole file), `lines:"f.txt"` (its lines, without `\n`); a missing file is a runtime error. `write:"f.txt",s` replaces the file with s and
  returns the byte count. `spawn:"cmd"` runs a shell command (its output goes straight to stdout/stderr) and returns its exit code
- math: `min max` (two values or one array), `abs pow sqrt floor ceil round gcd lcm sum prod` (`sum/prod` of `[]` are 0/1)
- `any all`: is some / every element of a bool, int or float array truthy (`[].any` is F, `[].all` is T); map first for a test: `xs{_ > 2}.any`
- bits: `rotl rotr popcount hex base`; `base:x,b` → x in base b (2..36 with digits `0-9a-z`, or 64 with the RFC 4648 alphabet `A-Za-z0-9+/`), no leading zeros, x read as unsigned (`base:11,2` → `"1011"`, `base:-1,16` → 16 f's); `hex:x` = `"0x"+base:x,16`; `int:s,b` parses s in base b (`int:"1011",2` → 11, `int:"ff",16` → 255); operators `& | ^ << >> >>>`; literals `0xff 0b101`
- strings: `lower upper trim replace chars split join ord chr find starts ends nums ws isupper islower isdigit`; the last four are T when every char of a non-empty string is whitespace, A–Z, a–z, 0–9: `"aB 3"[isupper]` → `"B"`, `s[ws]`; `"hello".starts:"he"` → `T`
- maps: `items` → `[(key, value)]` in hash order; `map:pairs` is its inverse, `[(key, value)]` (or a set of pairs) → map (a later pair wins): `map:flatten:grid{r = @; _{((r,@),_)}}` → `(row,col)` → cell
- arrays: `take sort rev uniq zip cross find bsearch sortby minkey maxkey flatten pairwise transpose tuple2 tuple3`; `transpose:[[1,2,3],[4,5,6]]` → `[[1,4],[2,5],[3,6]]`, on strings `transpose:["abc","def"]` → `["ad","be","cf"]` (a short row is skipped in the columns it lacks); `pairwise:[1,2,3]` → `[(1,2),(2,3)]`, on a string its characters: `"abc".pairwise` → `[("a","b"),("b","c")]`; `cross:a,b` → every `(x, y)` with x from a, y from b, a-major: `cross:0..2,[T,F]` → `[(0,T),(0,F),(1,T),(1,F)]`; `flatten` flattens one level of an array of arrays or of same-typed pairs or triples: `[[1,2],[3]]` and `[(1,2),(3,4)]` → `[1,2,3,4]`; `xs.sort:{_1>_2}` sorts with a comparer block on two elements (bool: first goes before second; int: negative/zero/positive like `{_1-_2}`; stable); `maxkey:ws,{#_}` → the longest word (first one on ties), `minkey` the shortest; on a map, `c.maxkey` is the key with the largest value
- grids: cells are `(row,col)` int tuples. `nb4:(r,c)` → `[(r-1,c),(r,c+1),(r+1,c),(r,c-1)]` (up, right, down, left); `nb8:(r,c)` → the 8 surrounding cells clockwise from up-left: `(r-1,c-1),(r-1,c),(r-1,c+1),(r,c+1),(r+1,c+1),(r+1,c),(r+1,c-1),(r,c-1)`. Bounded: `nb4:p,w,h` (width, then height) keeps only cells with `0<=r<h & 0<=c<w`; `nb4:p,g` takes the bound from a grid `g` (`[str]` or `[[T]]`: `#g` rows, `#(g@0)` columns). BFS step: `q{p = _; (nb4:p,g)[!(d@?_) & (g@_1)@_2!="#"]{d@_ = d@p+1; q+=_}}`

## 8. Examples
```
1..11[!_%2]{^^2}/+                                   # sum of squares of evens
1..101{println: !_%15?"FizzBuzz":!_%5?"Buzz":!_%3?"Fizz":_}
n = int:read; println: 1..(n+1)/*                       # factorial
```

## 9. Grammar (EBNF)
```
program   = { stmt TERM } ;
TERM      = NEWLINE | ";" ;
stmt      = "??" stmt | fndef | assign | while | expr ;
fndef     = IDENT param { param } "=" ( "{" block "}" | expr ) ;
param     = IDENT | "(" pname { "," pname } ")" ;   (* pname = IDENT | "_" *)
while     = expr "~{" block "}" ;
assign    = lvalues "=" exprlist ;
lvalues   = lvalue { "," lvalue } ;
lvalue    = IDENT { "@" prefix } ;
exprlist  = expr { "," expr } ;
expr      = lvalue ":=" expr | call ;
call      = FUNC ":" exprlist | ternary ;   (* FUNC = IDENT bound to block/built-in *)
ternary   = logic [ "?" expr [ ":" expr ] | ( "?{" arms "}" | "?[" arms "]" ) ] ;
arms      = arm { TERM arm } ;  arm = "_" "=>" stmt | test { "," test } [ "=>" stmt ] ;
test      = [ CMPOP ] expr ;                 (* bare CMPOP only with a subject *)
logic     = not { ("&" | "|") not } ;
not       = "!" not | compare ;
compare   = add [ CMPOP add ] ;
add       = mul { ("+" | "-") mul } ;
mul       = pow { ("*" | "**" | "/" | "%") pow } ;
pow       = prefix [ "^^" pow ] ;
prefix    = ("-" | "#") prefix | postfix ;
postfix   = primary { "{" block "}" | "[" block "]"
                    | "/" ( "{" block "}" | BINOP )
                    | "\" ( "{" block "}" | BINOP )
                    | "@" prefix
                    | "." IDENT } ;
primary   = atom [ ".." rangeend ] ;
rangeend  = rterm { ("+" | "-") rterm } ;  rterm = atom { ("*" | "**" | "/" | "%") atom } ;
atom      = [ "-" | "#" ] ( INT | IDENT | "(" expr ")" | "(" stmts ")" ) | base ;   (* "(" stmts ")" needs a top-level ";" *)
base      = INT | FLOAT | STRING | BOOL | IDENT | "_" | "_" INT | "$" | "@"
          | ( IDENT | "_" | "_" INT | "@" ) "'" { "'" }     (* previous iteration, no spaces *)
          | "[" [ exprlist ] "]" | "[" pairs "]" | "[->]"
          | ("%" | "Q" | "PQ") "[" [ exprlist ] "]"
          | "(" expr ")" | "{" block "}" ;
pairs     = expr "->" expr { "," expr "->" expr } ;
block     = [ BINOP ] stmts ;      (* leading BINOP => operator section *)
stmts     = stmt { TERM stmt } ;
```

## 10. Decisions log
Resolved: exclusive ranges, no fold seed, floor int division, keep `^^`, by-reference capture, `!` below compare, range at primary level, `#` comment vs length, call-vs-ternary by binding, `**` repeats and `*` is always elementwise, int before a block is `0..n`, `str/str` splits, structural overloads before elementwise before conversion (operator matrix, §3).
