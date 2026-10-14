<h1 align="center">NS</h1>
<p align="center"><b>the fast golf language</b><br>
terse like a golf language · statically typed · compiled to native code via LLVM</p>

---

```ns
println:1..1000[!_%3|!_%5]/+                    # 233168          Project Euler #1
println:2..50[p=_; !2..p[!p%_]]                 # [2,3,5,7,11,…]  primes
println:1..11\*                                 # [1,2,6,24,…]    running factorials
println:(split:"the fast golf language"," ").sortby:{-#_}   # ["language",…]
1..101{println:!_%15?"FizzBuzz":!_%5?"Buzz":!_%3?"Fizz":_}
```

```ns
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}   # quicksort
```

No type annotations, yet every expression is typed at compile time. `{}` maps, `[]` filters, `/` folds, `\` scans,
and they fuse into a single loop. Memory is reference counted; there is no GC.

**[Learn NS in Y minutes](#learn-ns-in-y-minutes)** · **[Syntax tour (slides)](docs/ns-syntax-tour.pdf)** · **[Language spec](src/SPEC.md)** · **[Examples](src/examples)**: Dijkstra, BFS, SHA-512, top-k, quicksort, …

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

## Learn NS in Y minutes

Save the code below as `learnns.ns` and run it with `ns learnns.ns`. Every
`# =>` comment is the line it prints.

```ns
# Single-line comments start with "# " (hash, then a space).
# A "#" glued to something else is the length operator, so mind the space:
# "####" banners are not comments.

# ==================================================
# 1. Primitives and operators
# ==================================================

# Four primitive types: int (64-bit), float (64-bit), string, bool.
# You never write a type. Every expression still has one, fixed at compile time.
println:42                  # => 42
println:3.14                # => 3.14
println:"hello"             # => hello
println:T                   # => T    (true)
println:F                   # => F    (false)

# Calling a function is "name:args". Arguments run to the end of the statement.
println:1+2*3               # => 7
println:2^^10               # => 1024   (power is ^^, since ^ is xor)
println:7/2                 # => 3
println:-7/2                # => -4     (integer / and % are floor division)
println:-7%2                # => 1
println:7.0/2               # => 3.5

# Comparison: = is equality, != is not-equal.
println:1=1                 # => T
println:1!=1                # => F
println:2<3                 # => T

# ! is "not". It binds looser than comparison, like Python's not.
println:!1=2                # => T      (!(1=2))
println:!0                  # => T

# & | ^ are logic on bools and bitwise on ints.
println:T&F                 # => F
println:6&3                 # => 2
println:6|3                 # => 7
println:6^3                 # => 5
println:1<<4                # => 16
println:0xff                # => 255
println:0b101               # => 5

# ==================================================
# 2. The left operand wins
# ==================================================

# There are no type errors between primitives. Instead, for any binary
# operator the LEFT operand's type is the result type, and the right
# operand is converted to it.
println:"1"+1               # => 11     (int -> string, then concatenate)
println:1+"1"               # => 2      (string -> int, then add)
println:1+2.7               # => 3      (float -> int truncates)
println:1.0+2               # => 3.0
println:"ab"*3              # => ababab
println:"9 lives"+0         # => 9 lives0
println:0+"9 lives"         # => 9      (strtol-style: longest valid prefix)

# Explicit casts are just functions named after the type.
println:int:"42"+1          # => 421    (args run to the end: int:("42"+1))
println:(int:"42")+1        # => 43
println:str:3.5             # => 3.5
println:bool:0              # => F

# ==================================================
# 3. Variables and statements
# ==================================================

# A variable's type is set by its first assignment.
x = 5
x = "12"                  # converted to int, x is still an int
println:x+1                 # => 13

# Several statements on one line: separate them with ;
a = 1; b = 2; println:a+b   # => 3

# Swap with parallel assignment.
a,b = b,a
println:"{a} {b}"           # => 2 1    ({expr} inside a string interpolates)

# Compound assignment works for every operator.
x += 1; x *= 2; x ^^= 2
println:x                   # => 676

# A line ending in an operator or comma keeps going.
t = 1 +
  2
println:t                   # => 3

# ==================================================
# 4. Arrays and ranges
# ==================================================

xs = [3,1,4,1,5]
println:xs                  # => [3,1,4,1,5]
println:#xs                 # => 5      (# is length)
println:xs@0                # => 3      (@ indexes)
println:xs@-1               # => 5      (negative counts from the end)
println:xs@1..3             # => [1,4]  (slice)
xs@0 = 9                  # set an element
println:xs++[2,6]           # => [9,1,4,1,5,2,6]   (concatenate)
println:xs++7               # => [9,1,4,1,5,7]     (append)

# Ranges exclude the end. The end may be arithmetic.
println:1..5                # => [1,2,3,4]
println:0..5-1              # => [0,1,2,3]
println:0..xs               # => [0,1,2,3,4]   (an array as the end means its length)
println:[0]*4               # => [0,0,0,0]

# Arithmetic broadcasts over arrays.
println:[1,2,3]*2           # => [2,4,6]
println:[1,2,3]+[10,20,30]  # => [11,22,33]

# Arrays and strings are values: assignment copies.
ys = xs; ys@0 = 0
println:xs@0                # => 9

# ==================================================
# 5. Blocks: map, filter, fold, scan
# ==================================================

# A block is code in {}. Inside it, _ is the current element and bare @ is
# the current index. Four postfix operators take a block:
println:1..6{_*_}           # => [1,4,9,16,25]     x{...}  map
println:1..10[_%3=0]        # => [3,6,9]           x[...]  filter
println:1..6/{$+_}          # => 15                x/{...} fold, $ is the accumulator
println:1..6\{$+_}          # => [1,3,6,10,15]     x\{...} scan (running fold)
println:"abc"{"{@}{_}"}     # => ["0a","1b","2c"]

# A body that starts with an operator gets _ filled in on the left.
println:1..6{*10}           # => [10,20,30,40,50]
println:1..10[>6]           # => [7,8,9]

# A body that is just a function name calls it on _.
println:["1","22"]{int}+1   # => [2,23]

# With pairs or tuples as elements, _1 _2 ... destructure them.
println:[1,4,9,16].pairwise{_2-_1}       # => [3,5,7]
println:(zip:[1,2],[3,4]){_1*_2}         # => [3,8]

# Fold and scan also take a bare operator.
println:1..6/+              # => 15      sum
println:1..6/*              # => 120     product
println:[3,8,2]/>           # => 8       max
println:[3,8,2]/<           # => 2       min
println:1..6\*              # => [1,2,6,24,120]

# They chain, and the compiler fuses the chain into a single loop.
println:1..11[!_%2]{^^2}/+  # => 220     sum of squares of the evens

# A map is also a for-each loop.
1..4{println:_}             # => 1 / 2 / 3, one per line

# Strings act as arrays of one-character strings for these operators.
println:"hello"[!_="l"]     # => heo    (filtering a string gives a string)
println:#"hello"            # => 5

# ==================================================
# 6. Control flow
# ==================================================

# The ternary is the if/else.
n = 15
println:n%2 ? "odd" : "even"                        # => odd
n>10 ? println:"big"                                # => big   (no else: runs only when true)
println:!n%15?"FizzBuzz":!n%5?"Buzz":!n%3?"Fizz":n  # => FizzBuzz

# Parentheses with ";" inside group statements; the value is the last one.
a = 0; b = 0
n>10 ? (a = 1; b = 2) : 0
println:"{a} {b}"           # => 1 2

# "cond ~{ body }" is a while loop. cond is re-checked before every pass.
n = 27; c = 0
n>1 ~{
  n = n%2 ? 3*n+1 : n/2
  c += 1
}
println:c                   # => 111   (Collatz steps for 27)

# "x ?{ ... }" is a match. Arms are tried in order; the first one wins.
n = 3
println:n ?{
  1 => "one"
  2, 3 => "few"             # any of several values
  >9 => "big"               # an arm starting with a comparison tests n
  _ => "some"               # default
}                           # => few
# Without a subject, each arm is a condition.
println:?{ n<0 => "neg"; n=0 => "zero"; "pos" }   # => pos
# An array subject maps the match over its elements; ?[ ] filters with it.
println:[95,85,10] ?{ >=90 => "A"; >=80 => "B"; "C" }   # => ["A","B","C"]
println:[1,2,5,9,12] ?[ 1, 2; >8 ]                       # => [1,2,9,12]
# An array-literal pattern compares the whole array.
println:[] ?{ [] => "empty"; _ => "other" }             # => empty

# ==================================================
# 7. Functions
# ==================================================

# A function is a block in a variable. One argument is _, more are _1, _2, ...
sq = {_*_}
add = {_1+_2}
println:sq:7                # => 49
println:add:3,4             # => 7

# x.f is the same as f:x, which makes pipelines read left to right.
println:5.sq                # => 25
[5,3,8].sort.println        # => [3,5,8]

# The last expression in a block is its value. Blocks may recurse.
fib = {_<2 ? _ : (fib:_-1)+(fib:_-2)}
println:fib:20              # => 6765

# Blocks capture variables by reference.
s = 0
[1,2,3]{s += _}
println:s                   # => 6

# No parameter types: each call site gets its own compiled version.
println:sq:1.5              # => 2.25

# Quicksort in one line.
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}
println:q:[3,6,1,8,2,9,2]   # => [1,2,2,3,6,8,9]

# ==================================================
# 8. Tuples
# ==================================================

tu = (1,"a",2.5)          # up to 9 elements of any types, immutable
println:tu@1                # => a
i,w,f = tu                # destructure
println:"{i}{w}"            # => 1a
println:(1,"b")<(1,"c")     # => T    (lexicographic compare)
k,v = tuple2:split:"k=v","="  # first 2 array elements as a tuple
println:v                   # => v

# ==================================================
# 9. Maps, sets, queues
# ==================================================

# Hash map. A missing key reads as the zero value.
m = ["a"->1, "b"->2]
m@"c" = 3
println:m@"a"               # => 1
println:m@"zzz"             # => 0
println:m@?"b"              # => T    (@? is "contains")
m -= "b"
println:#m                  # => 2

# Empty literals get their type from the first use.
cnt = [->]
"mississippi"{cnt@_ += 1}
println:cnt.keys.sort{"{_}{cnt@_}"}.join:" "   # => i4 m1 p2 s4

# In a map block, _ is the value and @ is the key.
println:cnt[>3].keys.sort   # => ["i","s"]

# Hash set.
st = %[1,2,3]
st += 4
println:st@?4               # => T
println:#(st & %[2,4,9])    # => 2   (& | - are intersect, union, difference)

# FIFO queue.
qu = Q[1,2]
qu += 3
println:qu.pop              # => 1
println:#qu                 # => 2

# Priority queue (min-heap). Tuples order by their first element.
pq = PQ[(5,"e"),(1,"a")]
pq += (3,"c")
d,w = pq.pop
println:w                   # => a
pq ~{ println:pq.pop@1 }    # => c / e   (an empty collection is falsy)

# Maps, sets and queues are references: assignment shares them.
m2 = m; m2@"z" = 26
println:m@"z"               # => 26

# ==================================================
# 10. Graphs
# ==================================================

# Undirected G[...] and directed DG[...]; an edge is (u,v) or (u,v,weight).
g = G[(1,2,1),(2,3,5)]     # one edge shape per literal
println:(g@2).sort          # => [1,3]   neighbours
println:g@(3,2)             # => 5       edge weight (0 if absent)
println:g@?(1,3)            # => F       has edge
g += (3,4)
g -= 1                      # remove node 1 with its edges
println:#g                  # => 3       node count
println:g.edges.sort        # => [(2,3,5),(3,4,1)]
dg = DG[(1,2),(2,3)]
println:(-dg).edges.sort    # => [(2,1,1),(3,2,1)]   reversed
# Nodes may be any hashable value, e.g. (row,col) grid cells.

# ==================================================
# 11. Standard library
# ==================================================

println:split:"a b c"," "           # => ["a","b","c"]
println:["x","y"].join:"-"          # => x-y
println:upper:"hi"                  # => HI
println:replace:"a-b","-","+"       # => a+b
println:ord:"a"                     # => 97
println:chr:98                      # => b
println:"hello"@?"ll"               # => T
println:[3,1,2].rev                 # => [2,1,3]
println:uniq:[3,1,3,2]              # => [3,1,2]
println:zip:[1,2],["a","b"]         # => [(1,"a"),(2,"b")]
println:sortby:["ccc","a","bb"],{#_}  # => ["a","bb","ccc"]
println:max:4,9                     # => 9
println:sqrt:16.0                   # => 4.0
println:gcd:84,36                   # => 12
println:hex:255                     # => ff
println:"hello".starts:"he"         # => T   (also: ends)
println:nums:"x=10, y=-3"           # => [10,-3]   every integer in a string
println:pairwise:[1,2,3]            # => [(1,2),(2,3)]
println:flatmap:[[1,2],[3]]         # => [1,2,3]

# Input: read gets one line of stdin ("" at EOF), lines gets all of them.
#   n = int:read
#   ls = lines
#   s = read:"f.txt"     whole file; lines:"f.txt" gives its lines
# Other files: import "lib.ns" splices one in (each file only once).
# Run with --debug to print every assignment as "line: name = value".
```
