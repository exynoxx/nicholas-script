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
| comment      | `# ...` to end of line                 | `#` followed by space or end of line; otherwise `#` is length |

### Statement termination
- A **newline** ends a statement.
- `;` ends a statement when several share one line: `a=1; b=2; println:a+b`.
- A newline does **not** end a statement when inside open `(` or `[`, or when the line ends with a binary operator or `,`.
- Inside `{ }` the same rules apply; statements in a block are separated by newline or `;`.

## 2. Types

v1 primitive types: `int` (i64), `float` (f64), `string`, `bool`.
Composite: arrays `[T]` (homogeneous), functions (blocks).
Deferred to v2: `uint`, `int8..int64`, explicit width ints.

### Inference
Each variable's type is fixed by its first assignment. Re-assigning a value of another type converts it to the variable's type (implicit conversion, below).

### Implicit conversion — the left operand rules
For any binary operator, **the left operand's type is the result type**; the right operand is converted to it.

```
"1"+1     # "11"   (int -> string, then concat)
1+"1"     # 2      (string -> int, then add)
1+2.7     # 3      (float -> int, truncates)
1.0+2     # 3.0
"a"*3     # "aaa"  (string * int = repeat)
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
| 0 primary | `a..b` range (start is an atom; end is `+ - * / %` over atoms, so `0..n-1` ends at n-1; postfix `{}` `[]` `/+` still apply to the range) | none |
| 1 postfix | `x{…}` map, `x[…]` filter, `x/{…}` `x/op` fold, `x\{…}` `x\op` scan, `x@i` index, `.name` method call | left |
| 2 prefix  | `-x` `#x` (length) | right |
| 3 power   | `^^` | right |
| 4 mul     | `* / %` | left |
| 5 add     | `+ - ++` | left |
| 5.5 shift | `<< >>` (ints; `>>` is arithmetic) | left |
| 7 compare | `= != < > <= >=` | none |
| 7.5 not   | `!x` (binds looser than compare, like Python `not`: `!_%2` = `!(_%2)`) | right |
| 8 logic   | `&` then `^` (xor) then `\|` (short-circuit on bools; bitwise on ints; set ops on sets) | left |
| 8.5 coalesce | `a ?? b` (b when a is a missing map key, see maps) | right |
| 9 ternary | `x ?{…}` `x ?[…]` match (§5); `c ? a : b`; `c ? a` without `:` runs `a` only when `c` holds (value 0); branches of unrelated types run as a statement (value 0) | right |
| 10 call   | `f:a,b` (args extend to end of statement / closing bracket). `IDENT:` is a call only if the name is bound to a block or built-in; otherwise `:` belongs to a ternary | right |
| 11 assign | `x = e`, `a,b = b,a` | right |

`=` in expression position is equality; at statement start with an lvalue on the left it is assignment.

### Arrays and ranges
```
x = [1,2,3]
r = 1..10        # range, end exclusive: 1..9
x@0              # index; negative counts from end: x@-1
x@1..3           # slice (elements 1,2); `@` takes a range operand
0..xs            # an array as a range end means its length: 0..#xs (also in slices)
#x               # length
```

### Collection operators

| Op       | Syntax          | Example      | Meaning |
|----------|-----------------|--------------|---------|
| map      | `x{body}`       | `x{_^^2}`    | new array of body results |
| filter   | `x[body]`       | `x[_>2]`     | keep items where body is truthy |
| fold     | `x/{body}`      | `x/{$+_}`    | `$` = accumulator, seeded with first element; no seed syntax, folding an empty collection is a runtime error |
| op-fold  | `x/op`          | `x/+`        | sum, `x/*` product, `x/>` max, `x/<` min, `x/&` all, `x/\|` any |
| scan     | `x\{body}` `x\op` | `x\+`      | running fold, returns array |

`/op` is a fold only when no operand follows the op (`x/-1` is division).
Disambiguation: `[` directly after an expression (no space) is a filter; otherwise it starts a literal. `/` followed by `{` (spaces allowed: `x / {$+_}`) or an operator token is a fold; otherwise division.

### Block shortcuts
- **Operator section**: a block body starting with a binary operator gets `_` on its left: `x{*2}`, `x[>2]`, `x{^^2}`. `-` never starts a section (`x{-1}` is the constant -1).
- **Function section**: a map, filter or plain block whose whole body is a function name calls it on `_`: `x{int}` is `x{int:_}`, `x[even]`, `sortby:xs,{str}`.
- Nested blocks: the innermost block owns `_`, `@`, `$`.
- `_` current element, `@` (bare) current index, `$` fold accumulator.
- `_1`, `_2`, … for multi-argument blocks. Inside a map/filter/fold/scan block they destructure the element instead (`_1` = `_@0`): `xs.pairwise{_2-_1}`, `(zip:a,b){_1*_2}`.
- Last expression in a block is its value.

### Array concat / append
`a++b` concatenates two arrays; `a++x` appends an element (converted to the element type). Arrays compare lexicographically with `= != < > <= >=` and return bool (comparisons do not broadcast).

### Broadcasting
Arithmetic between an array and a scalar, or two arrays of equal length, applies elementwise:
`x*2`, `x+y`, `x^^2`. Left-operand conversion rules apply per element. Unequal lengths are a runtime error; `scalar op array` is a compile error.

### Strings
Strings are byte strings. They behave as arrays of 1-character strings for `{}`, `[]`, `/`, `\`, `@`, `#` only.
Strings never broadcast: `+` concatenates, `*int` repeats. `ord:"a"` → 97, `chr:97` → `"a"`.

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

### Graphs

| Type | Literal | Empty | Notation in docs |
|------|---------|-------|------------------|
| undirected graph | `G[(1,2),(2,3)]` | `G[]` | `G T` |
| directed graph | `DG[(1,2),(2,3)]` | `DG[]` | `G T` |

An edge is `(u,v)` or `(u,v,w)` with an int weight (default 1); one literal uses one edge shape. `G:edges` / `DG:edges`
convert an array of edges. Nodes may be any hashable type, e.g. `(r,c)` grid cells. Graphs are reference types.

| Op | Meaning |
|----|---------|
| `g@u` | neighbours of `u` (array; `[]` if absent) |
| `g@(u,v)` | weight of edge u→v, 0 if absent |
| `g@?u`, `g@?(u,v)` | has node / has edge |
| `g+=(u,v)`, `g+=(u,v,w)` | add edge (and its nodes), in place |
| `g-=(u,v)`, `g-=u` | remove edge (nodes stay) / node with its edges, in place |
| `#g`, `#g@u` | node count, degree |
| `g{…}` | map over nodes, `_` = neighbours, `@` = node; returns a map node → result |
| `g[…]` | subgraph induced by the nodes passing the filter |
| `g\|h` `g&h` `g-h` | new graph: union / intersection / difference of edges (`g`'s weights and directedness) |
| `-g`, `g.rev` | edges reversed |
| `g.nodes`, `g.edges` | arrays; edges as `(u,v,w)`, undirected ones once with `u <= v` |

An argument that matches the node type is a node, otherwise a 2- or 3-tuple is an edge. A graph prints as its literal.

Maps, sets and queues are **reference types** (mutated in place, shared on assignment). Arrays and strings remain values.

| Op                 | Map `m`             | Set `s`           | Queue / PQ `q`               |
|--------------------|---------------------|-------------------|------------------------------|
| get / peek         | `m@k` (null if missing), `m@k ?? d` | —       | `q@0` front/top without removing (Q also: `q@-1` back) |
| set / insert       | `m@k = v`           | `s+=x`            | `q+=x` (Q: push back; PQ: insert by priority) |
| remove             | `m-=k`              | `s-=x`            | `q.pop` returns and removes front/top |
| contains           | `m@?k`              | `s@?x`            | `q@?x` (linear scan)         |
| size               | `#m`                | `#s`              | `#q`                         |
| map / filter / fold| `m{…}` `m[…]` `m/…` | `s{…}` `s[…]` `s/…` | `q{…}` etc. (Q: front to back; PQ: priority order; non-destructive) |
| union / inter / diff | —                 | `a\|b` `a&b` `a-b` | —                           |
| keys / values      | `m.keys` `m.vals`   | —                 | —                            |

In map blocks `_` is the value and bare `@` is the key (same as element/index for arrays).
`m{…}` returns a map with the same keys; `s{…}` returns a set; `m[…]`/`s[…]` return the same kind.
`@?` (contains) also works on arrays and strings: `"hello"@?"ll"`.
Conversions: `set:arr`, `Q:arr`, `PQ:arr`, `arr:s` (to array; map → array of keys).
Truthiness: an empty map/set/queue is falsy.

```
c = [->]; "hello"{c@_+=1}          # char counts
s = %[]; [3,1,3]{s+=_}; println:#s         # 2
q = PQ[[0,1]]; q ~{ d,n = q.pop; ... }    # Dijkstra skeleton
```

Lexing/typing rules for collections:
- `%[`, `Q[`, `PQ[` are single tokens, only in operand position. `->` uses maximal munch. `@?` is one token only after an operand (`x{@?1:0}` is bare `@` then ternary).
- Empty literals: the first use in source order fixes the element type. A literal that is never used with a type defaults to int. A use inside a block that is called at two different types is a compile error.
- `| & -` keep their single precedence level, so mixed set expressions need parens. On sets, the right operand's type decides the meaning: a set means union/inter/diff, an element means add/remove.
- `@` on a map takes a plain key: no negative indexes and no slices.
- Maps iterate and print in hash order. Reading a missing key gives null, which no value can hold: it is a runtime error
  unless caught by `m@k ?? d` (d when k is missing; m and k are evaluated once, no insert).
  Compound assignment is the exception: `m@k op= x` reads a missing key as the zero value (0, 0.0, `""`, F, `[]`), so `c@_+=1` counts.
  `??` is right-associative and binds looser than `|`: `(m@k??0)+1`. On anything but a map read it returns its left side.
- Popping or peeking an empty queue is a runtime error.
- Destructuring: N lvalues with one array on the right unpack it (`d,n = q.pop`). All right-hand expressions are evaluated before any assignment.

### Compound assignment
`x op= e` for every binary op (`+= -= *= /= %= ^^= &= |=`), meaning `x = x op e`, except that on maps/sets/queues every `op=` mutates in place, so aliases see the change. `m+=n` merges map `n` into `m`.

## 4. Functions
```
sq = {_^^2}
add = {_1+_2}
add:3,4            # call
x.sq               # method call: sq:x
x[>2]{*2}.sort.println
```
Blocks capture variables from the enclosing scope **by reference** (`s=0; x{s+=_}` updates `s`). In v1 blocks may not escape (be returned or stored in collections), so captures live on the stack.
A name is bound before its block body is checked, so blocks can recurse: `f={_<2?_:f:_-1}`.
A function's parameter types are inferred per call site (monomorphised at compile time).

`memo:{…}` memoizes a block (Python's `@lru_cache`, unbounded): each call first looks its arguments up in a cache,
keyed on the argument (or the tuple of arguments), and runs the body only on a miss. Each typed instance has its own cache.
Captured variables are not part of the key.
```
fib = memo:{_<2 ? _ : (fib:_-1)+(fib:_-2)}
fib:90                                       # 2880067194370816120, instantly
paths = memo:{_1=0 | _2=0 ? 1 : (paths:_1-1,_2)+(paths:_1,_2-1)}
```

## 5. Control flow
```
c ? a : b                  # ternary
c ? a                      # if without else
c ? (x = 1; y = 2) : 0     # statement group: `;` inside parens, value = last statement
1..10{println:_}             # for-each = map
cond ~{ body }             # while statement: whole `cond` expression re-evaluated each pass
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
```
Arms are separated by newline or `;` and tried in order; the first match wins. Inside a subject match `_` is the
subject (the element, for an array). If any pattern is an array literal (`[1,2] =>`, `[] =>`, `<[2] =>`), an array
subject is compared as a whole value instead of mapped over. An arm's result may be a statement (`5 => c += 1`, value 0). Without a default
the match behaves like `c ? a`: arms run for effect and the value is 0. In `?[ ]` an arm without `=>` keeps its
elements, `=> F` drops them, and `_ =>` decides the rest (default `F`). `?[` directly after an operand is a match
filter unless its `]` is followed by `:` (then it is a ternary with an array literal: `c?[1]:[2]`).

## 6. Statements
`import "file.ns"` splices in another file's statements (path relative to the importing file; each file is included once, so imports may repeat or be mutual).

```
x = expr
a,b = b,a                  # parallel assignment
expr                       # expression statement
```

Debug mode: `--debug` (on run/build/test) prints `line: name = value` after every assignment to a user variable, including `x@i = v` and compound assignments.

## 7. Built-ins and standard library
Language primitives (in the compiler): `print` (no newline), `println`, casts `int float str bool`, `#` length, `@?` contains, ranges and slices,
collection literals and `set: Q: PQ: arr:`, `pop`, `keys`, `vals`, tuples, `[x]*n`.
Printing and `str` show a string bare, but strings nested in arrays, maps and tuples are quoted: `println:["a"]` → `["a"]`. `join` does not quote.

Tuples: `(a,b)`, `(a,b,c)` … up to 9 elements of any types; immutable; `t@0` (literal index), `t._1` (1-based, = `t@0`), `a,b = t` or `(a,b) = t`; `tuple2:xs` `tuple3:xs` take the first 2 or 3 array elements; compare lexicographically; usable as map keys, set and PQ elements.
Repeat: `[x]*n` (a one-element array literal times an int) builds n copies; nested arrays are deep-copied.

Everything else is the **standard library, implemented in `runtime.c`**. Each function is declared there by a signature line
```
//@ name (param, ...) result [= c_symbol]
```
using `int float str bool`, type variables (`T`, `K`, …), `[t]`, `(a,b)` and blocks `fn(t)->r`. Overloads are tried in order
(an exact type match wins, otherwise the first one with conversions). Adding a function = writing it in C with its `//@` line;
the compiler needs no change. A block argument is passed to C as a callback, e.g. `bsearch:xs,{>=5}`, `sortby:ws,{#_}`.

Current library (see `grep '^//@' runtime.c`):
- I/O: `read` (a line, `""` at EOF), `lines` (all remaining stdin lines); zero-argument functions are called by bare name. With a path: `read:"f.txt"` (whole file), `lines:"f.txt"` (its lines, without `\n`); a missing file is a runtime error
- math: `min max` (two values or one array), `abs pow sqrt floor ceil round gcd lcm sum prod` (`sum/prod` of `[]` are 0/1)
- bits: `rotl rotr popcount hex`; operators `& | ^ << >> >>>`; literals `0xff 0b101`
- strings: `lower upper trim replace chars split join ord chr find starts ends nums`; `"hello".starts:"he"` → `T`
- graphs: `G DG nodes edges rev subgraph`, plus the operator forms above (`nbrs weight hasedge addedge deledge delnode gunion ginter gdiff`)
- arrays: `sort rev uniq zip find bsearch sortby minkey maxkey flatmap pairwise tuple2 tuple3`; `pairwise:[1,2,3]` → `[(1,2),(2,3)]`; `flatmap` flattens one level of an array of arrays or of same-typed pairs or triples: `[[1,2],[3]]` and `[(1,2),(3,4)]` → `[1,2,3,4]`; `maxkey:ws,{#_}` → the longest word (first one on ties), `minkey` the shortest; on a map, `c.maxkey` is the key with the largest value

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
stmt      = assign | while | expr ;
while     = expr "~{" block "}" ;
assign    = lvalues "=" exprlist ;
lvalues   = lvalue { "," lvalue } ;
lvalue    = IDENT { "@" prefix } ;
exprlist  = expr { "," expr } ;
expr      = call ;
call      = FUNC ":" exprlist | ternary ;   (* FUNC = IDENT bound to block/built-in *)
ternary   = logic [ "?" expr [ ":" expr ] | ( "?{" arms "}" | "?[" arms "]" ) ] ;
arms      = arm { TERM arm } ;  arm = "_" "=>" stmt | test { "," test } [ "=>" stmt ] ;
test      = [ CMPOP ] expr ;                 (* bare CMPOP only with a subject *)
logic     = not { ("&" | "|") not } ;
not       = "!" not | compare ;
compare   = add [ CMPOP add ] ;
add       = mul { ("+" | "-") mul } ;
mul       = pow { ("*" | "/" | "%") pow } ;
pow       = prefix [ "^^" pow ] ;
prefix    = ("-" | "#") prefix | postfix ;
postfix   = primary { "{" block "}" | "[" block "]"
                    | "/" ( "{" block "}" | BINOP )
                    | "\" ( "{" block "}" | BINOP )
                    | "@" prefix
                    | "." IDENT } ;
primary   = atom [ ".." rangeend ] ;
rangeend  = rterm { ("+" | "-") rterm } ;  rterm = atom { ("*" | "/" | "%") atom } ;
atom      = [ "-" | "#" ] ( INT | IDENT | "(" expr ")" | "(" stmts ")" ) | base ;   (* "(" stmts ")" needs a top-level ";" *)
base      = INT | FLOAT | STRING | BOOL | IDENT | "_" | "_" INT | "$" | "@"
          | "[" [ exprlist ] "]" | "[" pairs "]" | "[->]"
          | ("%" | "Q" | "PQ") "[" [ exprlist ] "]"
          | "(" expr ")" | "{" block "}" ;
pairs     = expr "->" expr { "," expr "->" expr } ;
block     = [ BINOP ] stmts ;      (* leading BINOP => operator section *)
stmts     = stmt { TERM stmt } ;
```

## 10. Decisions log
Resolved: exclusive ranges, no fold seed, floor int division, keep `^^`, by-reference capture, `!` below compare, range at primary level, `#` comment vs length, call-vs-ternary by binding.
