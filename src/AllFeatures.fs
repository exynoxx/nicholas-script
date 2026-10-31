/// Every feature, in registration order. Order matters where hooks are lists (lexer rules, typer interceptors,
/// prims, operator rules: first match wins) and for a feature's `init` that uses another's tables.
module AllFeatures

open Features

/// The optimiser's passes (src/features/opt), ordered by their own `order`.
let private optimiser =
    { Feature.empty with
        name = "optimiser"
        optPasses = [ Opt.Sat.pass; Opt.TailLoop.pass; Opt.Licm.pass; Opt.Fbip.pass; Opt.Cse.pass; Opt.Dce.pass; Opt.StrBuild.pass; Opt.Borrow.pass ] }

let all =
    [ Fusion.feature
      Ranges.feature
      Arrays.feature
      Strings.feature
      Tuples.feature
      Maps.feature
      Set.feature
      Queue.feature
      Queue.q
      Queue.pq
      Tree.feature
      Coll.feature // after Set: set algebra `s + t` wins over adding one element
      Sort.feature
      Interp.feature
      Match.feature
      Fold.feature
      Try.feature
      Memo.feature
      Lazy.feature
      Debug.feature
      optimiser ]

let mutable private registered = false
let registerAll () =
    if not registered then
        registered <- true
        List.iter Feature.register all
