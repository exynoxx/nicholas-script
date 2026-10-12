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

**[Learn NS in Y minutes](#learn-ns-in-y-minutes)** · **[Language spec](src/SPEC.md)** · **[Examples](src/examples)**: Dijkstra, BFS, SHA-512, top-k, quicksort, …

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
print:42                  # => 42
print:3.14                # => 3.14
print:"hello"             # => hello
print:T                   # => T    (true)
print:F                   # => F    (false)

# Calling a function is "name:args". Arguments run to the end of the statement.
print:1+2*3               # => 7
print:2^^10               # => 1024   (power is ^^, since ^ is xor)
print:7/2                 # => 3
print:-7/2                # => -4     (integer / and % are floor division)
print:-7%2                # => 1
print:7.0/2               # => 3.5

# Comparison: = is equality, ~= is not-equal.
print:1=1                 # => T
print:1~=1                # => F
print:2<3                 # => T

# ~ is "not". It binds looser than comparison, like Python's not.
print:~1=2                # => T      (~(1=2))
print:~0                  # => T

# & | ^ are logic on bools and bitwise on ints.
print:T&F                 # => F
print:6&3                 # => 2
print:6|3                 # => 7
print:6^3                 # => 5
print:1<<4                # => 16
print:0xff                # => 255
print:0b101               # => 5

# ==================================================
# 2. The left operand wins
# ==================================================

# There are no type errors between primitives. Instead, for any binary
# operator the LEFT operand's type is the result type, and the right
# operand is converted to it.
print:"1"+1               # => 11     (int -> string, then concatenate)
print:1+"1"               # => 2      (string -> int, then add)
print:1+2.7               # => 3      (float -> int truncates)
print:1.0+2               # => 3.0
print:"ab"*3              # => ababab
print:"9 lives"+0         # => 9 lives0
print:0+"9 lives"         # => 9      (strtol-style: longest valid prefix)

# Explicit casts are just functions named after the type.
print:int:"42"+1          # => 421    (args run to the end: int:("42"+1))
print:(int:"42")+1        # => 43
print:str:3.5             # => 3.5
print:bool:0              # => F

# ==================================================
# 3. Variables and statements
# ==================================================

# A variable's type is set by its first assignment.
x = 5
x = "12"                  # converted to int, x is still an int
print:x+1                 # => 13

# Several statements on one line: separate them with ;
a = 1; b = 2; print:a+b   # => 3

# Swap with parallel assignment.
a,b = b,a
print:"{a} {b}"           # => 2 1    ({expr} inside a string interpolates)

# Compound assignment works for every operator.
x += 1; x *= 2; x ^^= 2
print:x                   # => 676

# A line ending in an operator or comma keeps going.
t = 1 +
  2
print:t                   # => 3

# ==================================================
# 4. Arrays and ranges
# ==================================================

xs = [3,1,4,1,5]
print:xs                  # => [3,1,4,1,5]
print:#xs                 # => 5      (# is length)
print:xs@0                # => 3      (@ indexes)
print:xs@-1               # => 5      (negative counts from the end)
print:xs@1..3             # => [1,4]  (slice)
xs@0 = 9                  # set an element
print:xs++[2,6]           # => [9,1,4,1,5,2,6]   (concatenate)
print:xs++7               # => [9,1,4,1,5,7]     (append)

# Ranges exclude the end.
print:1..5                # => [1,2,3,4]
print:[0]*4               # => [0,0,0,0]

# Arithmetic broadcasts over arrays.
print:[1,2,3]*2           # => [2,4,6]
print:[1,2,3]+[10,20,30]  # => [11,22,33]

# Arrays and strings are values: assignment copies.
ys = xs; ys@0 = 0
print:xs@0                # => 9

# ==================================================
# 5. Blocks: map, filter, fold, scan
# ==================================================

# A block is code in {}. Inside it, _ is the current element and bare @ is
# the current index. Four postfix operators take a block:
print:1..6{_*_}           # => [1,4,9,16,25]     x{...}  map
print:1..10[_%3=0]        # => [3,6,9]           x[...]  filter
print:1..6/{$+_}          # => 15                x/{...} fold, $ is the accumulator
print:1..6\{$+_}          # => [1,3,6,10,15]     x\{...} scan (running fold)
print:"abc"{"{@}{_}"}     # => [0a,1b,2c]

# A body that starts with an operator gets _ filled in on the left.
print:1..6{*10}           # => [10,20,30,40,50]
print:1..10[>6]           # => [7,8,9]

# Fold and scan also take a bare operator.
print:1..6/+              # => 15      sum
print:1..6/*              # => 120     product
print:[3,8,2]/>           # => 8       max
print:[3,8,2]/<           # => 2       min
print:1..6\*              # => [1,2,6,24,120]

# They chain, and the compiler fuses the chain into a single loop.
print:1..11[~_%2]{^^2}/+  # => 220     sum of squares of the evens

# A map is also a for-each loop.
1..4{print:_}             # => 1 / 2 / 3, one per line

# Strings act as arrays of one-character strings for these operators.
print:"hello"[~_="l"]     # => heo    (filtering a string gives a string)
print:#"hello"            # => 5

# ==================================================
# 6. Control flow
# ==================================================

# The ternary is the if/else.
n = 15
print:n%2 ? "odd" : "even"                        # => odd
print:~n%15?"FizzBuzz":~n%5?"Buzz":~n%3?"Fizz":n  # => FizzBuzz

# "cond ~{ body }" is a while loop. cond is re-checked before every pass.
n = 27; c = 0
n>1 ~{
  n = n%2 ? 3*n+1 : n/2
  c += 1
}
print:c                   # => 111   (Collatz steps for 27)

# ==================================================
# 7. Functions
# ==================================================

# A function is a block in a variable. One argument is _, more are _1, _2, ...
sq = {_*_}
add = {_1+_2}
print:sq:7                # => 49
print:add:3,4             # => 7

# x.f is the same as f:x, which makes pipelines read left to right.
print:5.sq                # => 25
[5,3,8].sort.print        # => [3,5,8]

# The last expression in a block is its value. Blocks may recurse.
fib = {_<2 ? _ : (fib:_-1)+(fib:_-2)}
print:fib:20              # => 6765

# Blocks capture variables by reference.
s = 0
[1,2,3]{s += _}
print:s                   # => 6

# No parameter types: each call site gets its own compiled version.
print:sq:1.5              # => 2.25

# Quicksort in one line.
q = {p = #_ ? _@0 : 0; #_<2 ? _ : (q:_[<p])++_[=p]++(q:_[>p])}
print:q:[3,6,1,8,2,9,2]   # => [1,2,2,3,6,8,9]

# ==================================================
# 8. Tuples
# ==================================================

tu = (1,"a",2.5)          # up to 9 elements of any types, immutable
print:tu@1                # => a
i,w,f = tu                # destructure
print:"{i}{w}"            # => 1a
print:(1,"b")<(1,"c")     # => T    (lexicographic compare)

# ==================================================
# 9. Maps, sets, queues
# ==================================================

# Hash map. A missing key reads as the zero value.
m = ["a"->1, "b"->2]
m@"c" = 3
print:m@"a"               # => 1
print:m@"zzz"             # => 0
print:m@?"b"              # => T    (@? is "contains")
m -= "b"
print:#m                  # => 2

# Empty literals get their type from the first use.
cnt = [->]
"mississippi"{cnt@_ += 1}
print:cnt.keys.sort{"{_}{cnt@_}"}.join:" "   # => i4 m1 p2 s4

# In a map block, _ is the value and @ is the key.
print:cnt[>3].keys.sort   # => [i,s]

# Hash set.
st = %[1,2,3]
st += 4
print:st@?4               # => T
print:#(st & %[2,4,9])    # => 2   (& | - are intersect, union, difference)

# FIFO queue.
qu = Q[1,2]
qu += 3
print:qu.pop              # => 1
print:#qu                 # => 2

# Priority queue (min-heap). Tuples order by their first element.
pq = PQ[(5,"e"),(1,"a")]
pq += (3,"c")
d,w = pq.pop
print:w                   # => a
pq ~{ print:pq.pop@1 }    # => c / e   (an empty collection is falsy)

# Maps, sets and queues are references: assignment shares them.
m2 = m; m2@"z" = 26
print:m@"z"               # => 26

# ==================================================
# 10. Standard library
# ==================================================

print:split:"a b c"," "           # => [a,b,c]
print:["x","y"].join:"-"          # => x-y
print:upper:"hi"                  # => HI
print:replace:"a-b","-","+"       # => a+b
print:ord:"a"                     # => 97
print:chr:98                      # => b
print:"hello"@?"ll"               # => T
print:[3,1,2].rev                 # => [2,1,3]
print:uniq:[3,1,3,2]              # => [3,1,2]
print:zip:[1,2],["a","b"]         # => [(1,a),(2,b)]
print:sortby:["ccc","a","bb"],{#_}  # => [a,bb,ccc]
print:max:4,9                     # => 9
print:sqrt:16.0                   # => 4.0
print:gcd:84,36                   # => 12
print:hex:255                     # => ff

# Input: read gets one line of stdin ("" at EOF), lines gets all of them.
#   n = int:read
#   ls = lines
# Other files: import "lib.ns" splices one in (each file only once).
```
