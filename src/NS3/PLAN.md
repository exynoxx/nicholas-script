# NS3 Compiler — Staged Implementation Plan

The compiler is written in F#. Source goes to textual LLVM IR, then `clang file.ll runtime.c -lm` produces a native binary.
The guiding goal is **low compiler complexity**. Surface sugar is removed early, every later stage sees a small core language, and the runtime does generic work wherever it can.

## Pipeline

```
source ─Lexer→ tokens ─Parser→ Surface AST ─Desugar→ Core ─Typer→ typed Core ─Lower→ lowered Core ─Codegen→ .ll ─clang→ binary
```

| Stage | File | In → Out | Dump flag |
|-------|------|----------|-----------|
| 1 Lex | Lexer.fs | string → Token list | `tokens` |
| 2 Parse | Parser.fs | tokens → Surface AST | `ast` |
| 3 Desugar | Desugar.fs | Surface → Core (untyped) | `core` |
| 4 Type | Typer.fs | Core → Core with types and `Conv` nodes, monomorphised function instances | `typed` |
| 5 Lower | Lower.fs | typed Core → Core containing only codegen-able nodes | `lowered` |
| 6 Emit | Codegen.fs | lowered Core → LLVM IR text | `ll` |

CLI: `ns3 <tokens|ast|core|typed|lowered|ll|build|run> file.ns3` and `ns3 test [dir]`.
Surface AST dumps use F# `%A`. Core dumps use a small pretty-printer (Core.fs) that prints NS3-like syntax annotated with types.

## Files

```
NS3.fsproj   Ast.fs (Token + Surface AST)   Core.fs (Core AST, Ty, printer)
Lexer.fs  Parser.fs  Desugar.fs  Typer.fs  Lower.fs  Codegen.fs  Program.fs
runtime.c    tests/*.ns3 + *.out (+ *.in)    run_tests via `ns3 test`
```

## Stage 1–2: Lexer and Parser

- Lexing follows SPEC §1, using the context flag `prevEndsExpr` and the space-before flag. The flag decides:
  - `[` filter vs literal
  - `{` map vs block
  - `@` index vs bare current index
  - `/op` and `\op` fold/scan vs division
  - `%[`, `Q[` and `PQ[` literal tokens
  - `#` comment vs length
  - `@?` contains
- The lexer also inserts the `TERM` token for statement ends.
- **Operator sections are removed in the lexer.** When a block opens (`{`, map `{`, filter `[`, fold `/{`, scan `\{`) and the next token is a binary operator other than `-`, the lexer inserts a `_` token. The parser never sees a section.
- The parser is recursive descent, with precedence climbing for binary operators.
  - The parser keeps a set of *function names*: the built-ins, plus every name assigned a block literal. It adds a name before parsing that block's body, so recursion works.
  - `IDENT:` is a call only for those names.
- The Surface AST is close to the source: interpolation, ranges, unary ops, methods, compound and parallel assignment, collection literals, fold-op and scan-op.

## Stage 3: Desugar — Surface → Core

Core has 11 expression forms and 5 statement forms:

```
E = Int | Float | Str | Bool | Var name | Arr [E] | Bin(op,E,E) | If(E,E,E)
  | Call(name,[E]) | Index(E,E) | Iter(kind,E,body) | Fn(id,body) | Conv E | Seq([S],E)
S = Assign(name,E) | SetIdx(E,E,E) | While(E,[S]) | Do E | Line n
```

Every core node has a mutable `t: Ty`, which the typer fills in.

| Surface | Core |
|---------|------|
| `-x` `~x` `#x` | `Call neg/not/len` |
| `x@?k` | `Call has [x;k]` |
| `a..b` | `Call range [a;b]` |
| `x@(a..b)` | `Call slice [x;a;b]` |
| `x.f:a` | `Call f [x;a]` |
| `"a{x}b"` | `Bin(+, Bin(+, Str "a", x), Str "b")`. The left-operand rule stringifies `x` |
| `[k->v,…]`, `[->]` | `Call mapof [Arr ks; Arr vs]` |
| `%[…]` `Q[…]` `PQ[…]` | `Call set/Q/PQ [Arr …]` |
| `x/op`, `x\op` | `Iter(fold/scan, x, [Do Bin(op, Var $, Var _)])` |
| `x op= e` | `Assign(x, Bin(op, Var x, e))` |
| `x@i op= e` | `SetIdx(x, i, Bin(op, Index(x,i), e))`. ponytail: `i` is evaluated twice |
| `a,b = e1,e2` | `t1=e1; t2=e2; a=t1; b=t2` |
| `a,b = e` | `t=e; a=t@0; b=t@1` |
| `c ? a : b` | `If` |
| `cond ~{…}` | `While` |
| block value | the block's last statement must be `Do`. If it is not, append `Do(Var x)` after an `Assign x`, or `Do(Int 0)` after a `While` |

A `Line n` statement goes before every statement, so later stages can report the line in errors.

## Stage 4: Typer

- **Types:** `TInt TFloat TStr TBool TArr TMap TSet TQ TPQ TFn id`, plus `THole (Ty option ref)` for empty literals.
  - A hole is bound by the first use that needs a concrete type: conversion, `++`, `SetIdx`, or a collection add.
  - A hole that is never bound defaults to `TInt`.
- **Scopes:**
  - Frames have parent links.
  - The global frame holds top-level variables, which are emitted as LLVM globals.
  - A function instance gets its own frame, whose parent is the frame where the block literal was *defined*.
  - Assigning to a name that is visible in an outer frame writes to it (by reference). Otherwise the assignment creates a local.
  - Every variable is renamed to a unique name (`x.12`).
- **Inline blocks (`Iter`):** these use the current frame. `_`, `@` and `$` become fresh locals for each `Iter`, shadowing the outer ones.
- **Functions:**
  - `Fn` has type `TFn id`.
  - A call `f:args` looks up or creates an instance keyed by `(id, argTys)`; this is the monomorphisation.
  - Recursion works because the return type starts as a hole, which the body binds.
  - A function frame records its *captures*: variables owned by a non-global ancestor frame.
  - After typing, a fixpoint pass adds callee captures to callers. A caller must forward every capture of the function it calls, unless it owns that variable.
- **Left-operand rule:** for `Bin`, the result type is the left type, and the right side is wrapped in `Conv`. Exceptions are fixed by a table in Typer:
  - comparisons return bool
  - `str*x` converts `x` to int (repeat)
  - `&` and `|` on sets are set operations, on ints bitwise, and otherwise logical on bools
  - `++` appends or concatenates arrays
  - on collections, `+`/`-` add or remove an element (mutating in place, returning the left side), and a map plus a map merges them
  - an array op an array, or an array op a scalar, broadcasts
- **Casts and built-ins:** `int float str bool` calls become `Conv`. Every built-in has its signature checked here.

## Stage 5: Lower — typed Core to minimal Core

The output contains **no** `Iter`, no broadcast `Bin`, no logical `&`/`|`, no set-operator `Bin`, and no `Conv` from a non-scalar type to bool.

| Construct | Lowered to |
|-----------|------------|
| `Iter` over an array | `Seq` with an index `While` loop. Result: map builds with `Call arrpush`; filter builds with `If`+push; fold/scan seed from element 0 and panic on empty |
| `Iter` over a string | first `Call chars s`, then the array path |
| `Iter` over a map/set/Q/PQ | first `Call toarr` (map keys; PQ in priority order), then the array path. A map binds `_ = m@key` and `@ = key`. Map and filter results are rebuilt as the source kind |
| broadcast | an `Iter map` over the array, then lowered as above |
| `a & b`, `a \| b` (bool) | `If` (short-circuit) |
| `s\|t`, `s-t` on sets | iterate `t`, adding to or removing from `s` |
| `s&t` on sets | iterate `toarr s`, removing elements not in `t` |
| arrays as values | `Call copy` around a `Var` of array type when it is assigned, passed as an argument, added to a collection, or put in an array literal |

## Stage 6: Codegen

- **Values:**

  | Type | Value |
  |------|-------|
  | int | `i64` |
  | float | `double` |
  | bool | `i1` |
  | everything else | `ptr` |

  - Strings are NUL-terminated `char*`. ponytail: `strlen` is O(n); add a length header if that ever matters.
  - Arrays, maps, sets, Q and PQ are pointers to runtime structs.
- **Slots:** every collection element is stored as a 64-bit *slot*: an int is stored as is, a float is bitcast, a bool is zero-extended, and a pointer goes through ptrtoint. Codegen has `toSlot`/`fromSlot`, so the runtime never needs to know element types…
- **Type descriptors:** …except for hashing, equality, ordering, printing and stringifying. For those, codegen passes a descriptor string.
  - Descriptor grammar: `I F S B`, `A<t>`, `M<k><v>`, `H<t>` (set), `Q<t>`, `P<t>`. For example, `MSAI` is a map from string to array of int.
  - The runtime walks the descriptor recursively. This means no per-type hash, compare or print functions are generated.
- **Emission:**
  - All locals live in `alloca`s collected into the entry block, and globals are `@g.name`.
  - `If` and `While` use plain branches plus result allocas. clang `-O2` runs mem2reg, so no phi nodes are hand-written.
- **Functions:** each instance becomes `define <ret> @f<id>_<n>(<args>, ptr <capture>...)`. Captures are pointers to the owner's alloca.
- **Arithmetic:**
  - `/` and `%` on ints are floor division and modulo, with a zero check that panics.
  - `^^` on ints calls `ns_ipow`; on floats it calls `llvm.pow.f64`.
- **Scalar conversions** are emitted inline (`sitofp`, `fptosi`, `icmp`, `zext`). Conversions to or from strings and non-scalars call the runtime.

## runtime.c ABI (C, every call by value: `i64`, `double` or `ptr`; `slot` = `int64_t`)

```
// generic, desc-driven
void  ns_print(const char*d, slot v)        char* ns_tostr(const char*d, slot v)
int64 ns_eq(d,a,b)  int64 ns_cmp(d,a,b)     int64 ns_len(d, slot v)
int64 ns_has(d, slot coll, slot x)          void* ns_toarr(d, slot coll)
void  ns_add(d, void*coll, slot x)          void  ns_del(d, void*coll, slot x)  // set/Q/PQ add, set/map del
void* ns_sort(const char*elemd, void*arr)   char* ns_join(const char*elemd, void*arr, char*sep)
// strings
char* ns_read()  int64 ns_s2i(char*)  double ns_s2f(char*)  char* ns_concat(char*,char*)
char* ns_repeat(char*,int64)  char* ns_str_at(char*,int64)  void* ns_chars(char*)
void* ns_split(char*,char*)  int64 ns_ord(char*)  char* ns_chr(int64)  char* ns_slice_s(char*,int64,int64)
// arrays {int64 len, cap; slot* data}
void* ns_arr_new(int64 n)  void ns_arr_push(void*,slot)  slot ns_arr_get(void*,int64)  void ns_arr_set(void*,int64,slot)
void* ns_arr_concat(void*,void*)  void* ns_arr_copy(void*)  void* ns_arr_slice(void*,int64,int64)
void* ns_range(int64,int64)  void* ns_rev(void*)
// map/set (open addressing), Q (linked list), PQ (binary min-heap); all structs start with int64 len
void* ns_map_new(const char*kd,const char*vd) slot ns_map_get(m,k) void ns_map_set(m,k,v) void* ns_map_vals(m) void ns_map_merge(m,n)
void* ns_set_new(const char*d)  void* ns_q_new(const char*d)  void* ns_pq_new(const char*d)
slot  ns_pop(d, coll)  slot ns_peek(d, coll)
// misc
int64 ns_ipow(int64,int64)  int64 ns_idiv(int64,int64)  int64 ns_imod(int64,int64)  void ns_panic(const char*)
```

Negative indexes and bounds checks are handled in `ns_arr_get/set`, `ns_str_at` and the slice functions. Memory is malloc'd and never freed (no GC in v1).

## Milestones

Each milestone runs end to end through **all six stages** and ends with golden tests passing under `ns3 test`.

| # | Scope |
|---|-------|
| M1 | ints, `+ - * / %`, vars, `print`, `;`/newline, comments, all dump flags |
| M2 | float, bool, string, conversions + casts, compare, logic, ternary, `^^`, interpolation |
| M3 | while, parallel assign, `read`, line continuation |
| M4 | arrays, ranges, index/slice/`#`, broadcasting, `++`, map/filter/fold/scan, strings as arrays |
| M5 | maps, sets, Q, PQ, `@?`, collection `+=`/`-=`, set ops, `pop keys vals sort rev split join ord chr` |
| M6 | function blocks, calls, monomorphisation, recursion, closures (captures) |

## Defaults adopted from QUESTIONS.md (minor items)

- `++` concatenates arrays and appends elements to them.
- Comparisons on arrays are lexicographic and return bool; they are not broadcast.
- `scalar op array` is an error.
- Strings inside arrays print unquoted.
- `&` and `|` are bitwise on ints and logical otherwise.
- Ternary: the else branch converts to the then-branch's type.
- Broadcasting arrays of unequal length panics.
- No early return.
- Reassigning a variable with a different scalar type converts; reassigning with a mismatched non-scalar type is an error.
