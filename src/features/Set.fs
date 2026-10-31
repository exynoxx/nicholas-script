/// Hash sets: `%[a, b]`, `set:xs`, `s - x`, set algebra `s & t`, `s | t` / `s + t`, `s - t` (in place when the
/// statement is `s = s op t`), `.any`/`.all`, and an owned copy when a fold accumulates into a set.
module Features.Set

open Ast
open Core
open Typer

let private isSet t = match resolve t with TCon("set", _) -> true | _ -> false

/// A set that the caller may update in place: copied unless nothing else references it.
let setCopy (x: E) =
    match resolve x.t with
    | TCon("set", [ el ]) when not (fresh x) -> stdCall "gunion" [ builtin "set" [ typed (Arr []) (TArr el) ]; x ]
    | _ -> x

let private bins op (inPlace: bool) (a: E) (b: E) =
    match op, resolve a.t with
    | ("&" | "|" | "+" | "-"), L when isSet L && isSet b.t ->
        Some(stdCall (match op with "&" -> "ginter" | "-" -> "gdiff" | _ -> "gunion") [ (if inPlace then a else setCopy a); conv b L ])
    | "-", (TCon("set", [ el ]) as L) -> Some(typed (Call("del", [ a; conv b el ])) L)
    | _ -> None

let private anyAll name = name, (function [ x ] when isSet x.t -> Some(builtin name [ stdCall "arr" [ x ] ]) | _ -> None)

let private coll = Coll.collection "set" "ns_set_new"

let feature =
    { coll with
        lex = [ Coll.literalOpener "%" ]
        prefix = [ "%[", Coll.literal "set" ]
        types = [ { con = "set"; show = (fun s ts -> "%" + s ts.Head); desc = fun d ts -> "H" + d ts.Head } ]
        bin = [ bins ]
        prims = coll.prims @ [ anyAll "any"; anyAll "all" ]
        // a fold's first element becomes its accumulator, which `+` then updates in place
        intercept = [ fun e -> match e.k with Call("%own", [ x ]) -> Some(setCopy (typeE x)) | _ -> None ] }
