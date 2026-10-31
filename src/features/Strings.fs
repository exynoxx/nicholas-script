/// String operators beyond `+` and comparison (the kernel's): `s / sep` splits, `s ** n` repeats, `-s` reverses,
/// `str:[strs]` joins, and `zip` over a string zips its characters.
module Features.Strings

open Core
open Typer

let private bins op (_: bool) (a: E) (b: E) =
    match op, resolve a.t with
    | "**", TStr -> Some(typed (Bin("*", a, conv b TInt)) TStr)
    | "/", TStr ->
        match resolve b.t with
        | TStr -> Some(stdCall "split" [ a; b ]) // `"a,b"/","` = split:"a,b",","
        | t -> err $"str / {showT t}: only str / str (split) is defined"
    | "*", TStr -> err "operator * not defined for str: use ** to repeat a string"
    | _ -> None

let feature =
    { Feature.empty with
        name = "strings"
        bin = [ bins ]
        prims =
          [ "str", (function [ x ] when resolve x.t = TArr TStr -> Some(stdCall "join" [ x ]) | _ -> None) // `str:["a","b"]` = "ab"
            "neg", (function [ x ] when resolve x.t = TStr -> Some(stdCall "rev" [ x ]) | _ -> None) // `-"abc"` is "cba"
            "zip", (function
                | [ a; b ] when resolve a.t = TStr || resolve b.t = TStr ->
                    let chars (x: E) = if resolve x.t = TStr then stdCall "chars" [ x ] else x
                    Some(stdCall "zip" [ chars a; chars b ])
                | _ -> None) ] }
