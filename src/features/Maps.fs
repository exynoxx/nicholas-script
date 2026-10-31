/// Map operations beyond literals-as-calls, indexing and `m@k = v` (the kernel's): the `[k -> v]` literal's
/// construction (`mapof`), `m ?? d` defaults, `m + m2` merge, `m - k`, `m ** 0`, `.entries`, and `.any`/`.all`
/// over the values. `m - k` uses Coll's `del`.
module Features.Maps

open Core
open Typer

let private bins op (_: bool) (a: E) (b: E) =
    match op, resolve a.t with
    | "+", (TMap _ as L) -> Some(typed (Call("merge", [ a; conv b L ])) L)
    | "-", (TMap(k, _) as L) -> Some(typed (Call("del", [ a; conv b k ])) L)
    | "**", TMap _ -> Some(stdCall "keep" [ a; conv b TInt ]) // `m**0`: empty in place
    | _ -> None

let private intercept (e: E) =
    match e.k with
    | Bin("??", a, b) ->
        // `m ?? d`: a missing key of m reads as d from now on (set in place, returns m)
        let a, b = typeE a, typeE b
        match resolve a.t with
        | TMap(_, v) ->
            (match resolve v with THole _ -> unify v b.t |> ignore | _ -> ())
            Some(stdCall "mapdef" [ a; conv b v ])
        | t -> err $"?? gives a map a default value (m ?? d), not {showT t}"
    | _ -> None

let feature =
    { Feature.empty with
        name = "maps"
        intercept = [ intercept ]
        bin = [ bins ]
        prims =
          [ "entries", (function [ x ] -> Some(match resolve x.t with TMap _ -> stdCall "items" [ x ] | _ -> x) | _ -> None)
            "mapof", (function [ ks; vs ] -> Some(typed (Call("mapof", [ ks; vs ])) (TMap(elemOf "mapof" ks, elemOf "mapof" vs))) | _ -> None)
            "any", (function [ x ] when (match resolve x.t with TMap _ -> true | _ -> false) -> Some(builtin "any" [ stdCall "vals" [ x ] ]) | _ -> None)
            "all", (function [ x ] when (match resolve x.t with TMap _ -> true | _ -> false) -> Some(builtin "all" [ stdCall "vals" [ x ] ]) | _ -> None) ]
        effects = [ "merge", Feature.Mutating ]
        kept = [ "merge", 1 ]
        llvm = [ "merge", fun g _ _ vs -> (g.rt "void" "ns_map_merge" [ "ptr", vs.[0]; "ptr", vs.[1] ] |> ignore; vs.[0]) ]
        llvmRaw =
          [ "mapof", fun g e args ->
                match args with
                | [ { k = Arr ks }; { k = Arr vs } ] ->
                    let m = g.rt "ptr" "ns_map_new" [ g.desc e.t ]
                    for (k, v) in List.zip ks vs do
                        let kv, vv = g.ex k, g.ex v
                        g.rt "void" "ns_map_set" [ "ptr", m; "ptr", g.spill kv k.t; "ptr", g.spill vv v.t ] |> ignore
                        g.release kv k.t
                        g.release vv v.t
                    m
                | _ -> failwith "mapof: expects literal key and value arrays" ] }
