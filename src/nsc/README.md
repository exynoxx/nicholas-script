# nsc — the NS compiler written in NS

`nsc` compiles NS to LLVM IR by itself (lexer, parser, type inference, code generation are all NS code) and only
calls out to `clang` to link the IR with the existing runtime (`runtime.c`, copied next to the binary).
No C was added for it; the command line is read through `spawn` + `/proc/$PPID/cmdline`.

```shell
./bootstrap.sh            # ns (F#) builds build/nsc1; nsc1 builds nsc2; nsc2 builds nsc3; nsc2.ll must equal nsc3.ll
./test.sh                 # golden tests (../tests, ../examples) through build/nsc2: all 91 pass
build/nsc2 f.ns [-o out]  # compile (writes out.ll, links out); --ll prints the IR instead
```

## Files (imported by `nsc.ns` in this order)

| file | does |
|---|---|
| `lex.ns` | text → tokens in parallel arrays `TK` kind / `TV` text / `TL` line (`file*1000000 + line`). `import` is spliced here. String interpolation `"a{x}b"` is lexed as the tokens `("a" + (x) + "b")`. |
| `parse.ns` | tokens → AST. A node is an int; fields in `NK NS NA NB NC NL`, child lists in `KIDS` (`kid: n, i`, `NN@n`). Port of `Parser.fs`. |
| `sig.ns` | reads the `//@` stdlib signatures from runtime.c into `SGNAME SGPAR SGRET SGSYM`. |
| `types.ns` | interned type ids (`TYK` kind I F S B A M H ?, children `TYA TYB`), holes + `unify`, `desc` (runtime descriptor), `llt` (LLVM type). |
| `gen.ns` | typing and LLVM emission in one walk. `ex: n` emits node n and returns its LLVM operand, leaving its type in `ET`. User functions are instantiated per argument types at their first call (`usercall`/`instance`); types still unknown while emitting are written as `` `id` `` / `` `did` `` placeholders and filled in by `fillin` at the end. |
| `tuple.ns` | tuples: types are cons lists of kind `T` ending in `TNIL`; value tuples (`V…`, LLVM structs) and boxed tuples (`U…`); destructuring, `f (a, b) = …` parameters, `_1 _2` in iteration blocks. |
| `colls.ns` | match `?{ }` / `?[ ]`, queues `Q[ ]` `PQ[ ]`, sets ops, sorted trees `BT[ ]` (kind `R`), collection conversions, `??`. |
| `bcast.ns` | array arithmetic (broadcasting), `[x]*n`, array conversions, map `+=`. |
| `blocks.ns` | blocks as values: captures by reference, stdlib callbacks (`void w(ptr env, ptr x, ptr out)` wrappers), `x${key}`, scans, `.take` on loops, `!f`, `memo:`. |
| `btree.ns` | sorted trees keyed by a block (`BT:xs,{key}`, descriptor `K<k><t>`). |
| `graph.ns` | graphs `G[ ]` / `DG[ ]` (kind `G`): neighbours, weights, edits, set ops, map/filter over nodes. |
| `nsc.ns` | driver. |

Memory: nsc-compiled programs never release (a compiler is short-lived); arrays keep value semantics by copying an
array read from a variable or element when it is stored (`owned`), and a function copies an array parameter it assigns to.

## Writing nsc's own NS: gotchas (each one bit us)

- A statement starting with `name =` or `name@i =` is an **assignment**. To test equality at statement start, wrap it:
  `(c = "x") ? …`, `(TK@P = "t") ~{ … }`. Same for the last expression of a `{}` body.
- `f a = x = y` defines f as the *comparison* `x = y`; use a `{ }` body for statements.
- A parenthesised group needs a `;` to be statements: `c ? (x += 1;)`, `(a = 1; b = 2)`; `(a = 1)` is a comparison.
- Call arguments run to the end of the statement or closing bracket: `[dref: t, spill: v, t]` passes everything to
  `dref`. Parenthesise nested calls: `[(dref: t), (spill: v, t)]`, `(isdig: c) ? …`, `bin: op, l, (shiftx: 0)`.
- `{` in a string starts interpolation: write `"\{"`. There is no `\r` escape (`"\r"` is `"r"`): use `chr: 13`.
- `++` and `+` share a precedence level: `xs ++ "a" + s` is `(xs ++ "a") + s` (broadcast!). Write `xs ++ ("a" + s)`.
- A multi-line ternary chain continues only on lines indented more than its first line, and a branch that is a
  multi-line `( … )` group breaks the chain after it — move such branches into helper functions.
- Inside a function, assigning a name that is a **global** writes the global. Globals are UPPERCASE, locals lowercase.
  A recursive function's locals are per call only if no global has that name.
- Don't name a function or a local like a stdlib function (`ends`, `len`, `max`…), an internal primitive of the F#
  compiler (`add append concat copy del has len mapof merge neg not peek push range repeat slice sort take tuple zero`),
  a reserved word (`while if for import` …) or another nsc function (`sym`, `tok`, `kid`, `mk`, …).
- `lines` and `read` are zero-argument calls when written bare, so they can't be variable names.
- `f: args` forward references only work within one file (for the F# `ns`, which builds stage 1). Code in an earlier
  file calls a function of a later-imported file with method syntax: `n.exmatch`, `op.collop: a, ta, b, tb, n`; a
  method call without arguments before a ternary `:` needs parentheses: `(n.excoll) : …`.
- A global map that is read before its first write (in compile order) needs its type fixed at top level:
  `X = ["" -> 0]` then `X = [->]`.

Debugging: `build/nsc1 f.ns --ll` shows the IR; compare with `ns ll f.ns` (the F# compiler). When nsc2 differs from nsc1,
the F#-compiled nsc1 is right and some pattern in nsc's source is miscompiled by nsc: reduce it to a small program.
