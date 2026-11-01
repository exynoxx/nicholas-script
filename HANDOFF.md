# Handoff — NS compiler: kernel + features vertical slicing (function calls step)

**Created:** 2026-10-31 13:09 UTC
**Working directory:** /home/nicholas/Dokumenter/git/nicholas-script
**Branch / commit:** `features-arch` @ `c26a570` (pushed to `origin/features-arch`), plus **uncommitted** work described below

## Objective

Restructure the NS compiler (F#, `src/`) into a bare-bones **kernel** (`src/kernel/`, one file per stage, knows no feature) plus **features** (`src/features/`, one file per language construct or compiler mechanism), each feature hooking into every stage it needs. Latest request: move function calls, array indexing and the type-specific LLVM codegen into features, so the kernel is mostly bare bones.

## Context

- This is a POC on the branch `features-arch`. The C backend (`CGen.fs`) and `ns explain` (`Explain.fs`) were deliberately deleted (commit `7c8f446`, user: "not relevant for this poc").
- **Another session works in the worktree `/home/nicholas/Dokumenter/git/ns-void` on `master`.** Don't touch `master` or that worktree. Don't reinstall the `ns` on PATH (`dotnet publish … -o ~/.local/share/ns`): that would replace the user's install with a branch build that has no C backend or `explain`.
- User memory: skip the nsc (self-hosted compiler) bootstrap and tests when verifying; commit after each finished step.
- The user asked for a push once already (`git push -u origin features-arch` done at `c26a570`). Push again only when asked.
- A worktree created for the Opt-split agent was based on an old commit (`eff9318`), not `HEAD`. If you delegate to worktree agents, have them check their base commit.
- **Architecture:**
  - `src/Feature.fs` defines the `Feature` record (one optional hook list per stage) and `register`, which fills each stage's tables.
  - `src/AllFeatures.fs` lists the features in registration order.
  - **Order matters:** list hooks are first-match-wins. Example: `Set.feature` must come before `Coll.feature`, otherwise `s + t` adds one element instead of taking the union.
  - Features compiled later may call earlier ones directly (`Lazy` → `Fusion.typeIter`, `Functions.userCall`).
  - CLAUDE.md "Layout: kernel + features" documents the hook tables.

## Current state

**Committed and pushed:**
- `7c8f446`: removes the C backend and `explain`.
- `ee568f9`: generic `TCon(name, args)` / `SCon` types replace `TSet`/`TQ`/`TPQ`/`TTree`.
- `fb05c6d`: the kernel + features architecture.
  - Features: fusion (`features/fusion/Lower.fs`, `Fusion.fs`), Ranges, Arrays, Strings, Tuples, Maps, Coll, Set, Queue (Q/PQ), Tree (BT), Sort, Interp, Match, Fold, Try, Memo, Lazy, Debug.
  - One file per optimiser pass in `features/opt/` (split by a subagent; IR byte-identical).
- `c26a570`: `x@i` and `x@i = v` typing and codegen move to Arrays/Strings/Maps (Tuples/Queue already had theirs).
  - New codegen hooks: `nodes` (array literal), `binops` (string concat/compare, value-tuple `=`), `convs` (str→int/float), `indexes`, `slots`.
  - Matching Feature fields: `setIndex`, `llvmNode`, `llvmBin`, `llvmConv`, `llvmIndex`, `llvmSlot`.

**Uncommitted (in the working tree, builds, 225/225 tests and 15/15 examples pass):** function calls move into the new `src/features/Functions.fs` (untracked).

- **Typer** (`src/kernel/Typer.fs`): `fnDefs`, `spreads`, `insts`, `instList`, `fixers`, `userCall`, `spreadBlock` and `propagateCaptures` moved out.
  - The kernel keeps `Frame`/`Inst` types, `lookup` (records captures), `emptyEls` (now public) and public `line`/`col`.
  - New kernel hooks: `blockCall` (mutable; stdlib block arguments) and `instances` (mutable; the program's instances).
  - Kernel typeAt no longer handles `Fn`, block calls, or bare `f` of an arity-0 block; Functions intercepts them.
- **Codegen** (`src/kernel/Codegen.fs`): user calls, callback wrappers, `envOf` and instance definitions moved out.
  - New: `blockArg` (mutable) and `programs` (emitters run before main).
  - `Api` gained `ptrOf`, `lendable`, `drop`, `stmts`, `varTy`, `inFunction`, `locals`, `define`, `defineLate`.
  - `noAssign` is public.
- **Parser** (`src/kernel/Parser.fs`): `FnDefAhead`, `ClauseAhead`, `Clause`, `Pat`, the clause state and the modifier handling in `Stmt` moved out.
  - New static hooks: `StmtForms`, `Declares`, `Ends`.
  - New members: `AddFunction`, `WithParams`, `FnAssignAhead`, `TokenCount`.
  - `foreign` and `assignable` are public.
- `features/Functions.fs` holds the parsing (per-parser clause state in a `ConditionalWeakTable`), typing and codegen. It's registered first in `AllFeatures.fs` and compiled right after `Feature.fs` in `NS.fsproj`.
- `features/Lazy.fs` and `features/Tree.fs` now `open Features.Functions` (they use `userCall`/`fnDefs`).
- The kernel is now ~3,020 lines (was ~4,700 before the architecture work).

**Leftover:** a temp worktree from the last (rejected) IR-diff command may exist at `/tmp/claude-1000/-home-nicholas-Dokumenter-git-nicholas-script/48ac89dd-30db-451a-a15b-2b65d99953ec/scratchpad/oldc` (detached at `ee568f9`). Remove it with `git worktree remove --force <path>`.

## Tried and rejected

- **`raise Exit` as the "not mine" signal in a typer intercept** — it throws on every typed node, which is too slow. Intercepts return `E option` instead.
- **Python-scripted auto-wrapping of match arms in `Some(...)`** — too fragile; the Lazy intercept was hand-written instead.
- **Static members named `Prefix`/`Postfix` on `Parser`** — they clash with the instance members `p.Prefix()`/`p.Postfix()`, hence the `*Hooks` names. They also need explicit type annotations (`static member val X: T = … with get`) or F# can't infer them.
- **`Coll` registered before `Set`** — breaks set algebra (4 test failures).

## Open questions / blockers

- The user rejected the IR-diff command mid-run, reason unknown. Before re-running it, ask whether they want that check, or use the tests only.
- How far "bare bones" should go. Still in the kernel:
  - map/filter/while syntax, `..` range parsing, `x'` previous-iteration state, `[k->v]` map-literal parsing, `[x]**n`, `@%`/`@?`
  - block desugaring (`EBlock` → `Fn`, `ret` guards, arity)
  - scopes/captures, conversions, ownership (`fresh`/`own`), refcounting
  - Repr layouts (value tuples), scalar binops, print/len/has/copy/panic
  - `runtime.c` (not split per feature)

  Ask which of these the user wants moved next.

## Next steps

1. Commit the uncommitted function-calls work on `features-arch`, e.g. "Function calls (definitions, base cases, modifiers, instances, codegen) move into features/Functions.fs", ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Don't push unless asked.
2. Update the CLAUDE.md "Layout: kernel + features" section:
   - Add `Functions` and the new hooks: Typer `blockCall`/`instances`/`setIndexers`; Codegen `nodes`/`binops`/`convs`/`indexes`/`slots`/`blockArg`/`programs`; Parser `StmtForms`/`Declares`/`Ends`.
   - Remove "base cases" from the "still in the kernel" list.
3. Remove the `oldc` temp worktree if it exists.
4. Report to the user: what moved, current kernel size, what remains in the kernel. Ask which remaining kernel parts to slice next (see the Open questions list).

## Success criteria

- From `src/`: `dotnet build -c Release` gives 0 errors; `dotnet run -c Release -- test tests` → `225/225 passed`; `dotnet run -c Release -- test examples` → `15/15 passed`.
- `grep -n "userCall\|FnDefAhead\|ClauseAhead\|UserCall(f" src/kernel/*.fs` finds no function machinery, only the `UserCall`/`BlockRef` node types in Core and their uses in the generic walkers.
- The work is committed on `features-arch`; `master` and `../ns-void` are untouched.

## Key files

- `/home/nicholas/Dokumenter/git/nicholas-script/src/Feature.fs` — the `Feature` record and `register`.
- `/home/nicholas/Dokumenter/git/nicholas-script/src/AllFeatures.fs` — feature list and registration order (order is semantics).
- `/home/nicholas/Dokumenter/git/nicholas-script/src/features/Functions.fs` — new, uncommitted: definitions, base cases, modifiers, instances, user-call and wrapper codegen.
- `/home/nicholas/Dokumenter/git/nicholas-script/src/kernel/Typer.fs` — hook tables near the top; `blockCall`/`instances` mutables; `check` at ~line 517.
- `/home/nicholas/Dokumenter/git/nicholas-script/src/kernel/Codegen.fs` — the `Api` record and hook tables at the top; `generate` runs `programs` before main.
- `/home/nicholas/Dokumenter/git/nicholas-script/src/kernel/Parser.fs` — static `*Hooks`, `StmtForms`/`Declares`/`Ends`, and the public members features use.
- `/home/nicholas/Dokumenter/git/nicholas-script/src/NS.fsproj` — compile order: kernel → `Feature.fs` → `features/Functions.fs` → other features → `AllFeatures.fs` → `Program.fs`.
- `/home/nicholas/Dokumenter/git/nicholas-script/CLAUDE.md` — project navigation; layout section needs the update from step 2.
- `/home/nicholas/Dokumenter/git/nicholas-script/ARCHITECTURE_REVIEW.md` — the user's untracked design notes that started this (features/ tiers); don't commit it unless asked.
