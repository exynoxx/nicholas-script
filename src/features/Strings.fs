/// Strings: `s@i` (a one-character string), `+` concatenation, `=` comparison, `str -> int/float` parsing,
/// `s / sep` splits, `s ** n` repeats, `-s` reverses,
/// `str:[strs]` joins, and `zip` over a string zips its characters.
module Features.Strings

open Core
open Typer

let private bins op (_: bool) (a: E) (b: E) =
    match op, resolve a.t with
    | "+", TStr -> Some(typed (Bin("+", a, conv b TStr)) TStr)
    | "**", TStr -> Some(typed (Bin("*", a, conv b TInt)) TStr)
    | "/", TStr ->
        match resolve b.t with
        | TStr -> Some(stdCall "split" [ a; b ]) // `"a,b"/","` = split:"a,b",","
        | t -> err $"str / {showT t}: only str / str (split) is defined"
    | "*", TStr -> err "operator * not defined for str: use ** to repeat a string"
    | _ -> None

let private isStr t = resolve t = TStr

let feature =
    { Feature.empty with
        name = "strings"
        bin = [ bins ]
        index = [ fun x i -> if isStr x.t then Some(typed (Index(x, conv i TInt)) TStr) else None ]
        llvmIndex = [ fun g _ x xv iv -> if isStr x.t then Some(g.rt "ptr" "ns_str_at" [ "ptr", xv; "i64", iv ]) else None ]
        llvmBin =
          [ fun g op a _ av bv ->
                match op with
                | _ when not (isStr a.t) -> None
                | "=" | "!=" ->
                    let r = g.rt "i64" "ns_str_eq" [ "ptr", av; "ptr", bv ]
                    let pred = if op = "=" then "ne" else "eq"
                    Some(g.instr $"icmp {pred} i64 {r}, 0")
                | "+" -> Some(g.rt "ptr" "ns_concat" [ "ptr", av; "ptr", bv ])
                | "*" -> Some(g.rt "ptr" "ns_repeat" [ "ptr", av; "i64", bv ])
                | _ -> None ]
        llvmConv =
          [ fun g v src dst ->
                match resolve src, resolve dst with
                | TStr, TInt -> Some(g.rt "i64" "ns_s2i" [ "ptr", v ])
                | TStr, TFloat -> Some(g.rt "double" "ns_s2f" [ "ptr", v ])
                | _ -> None ]
        prims =
          [ "str", (function [ x ] when resolve x.t = TArr TStr -> Some(stdCall "join" [ x ]) | _ -> None) // `str:["a","b"]` = "ab"
            "neg", (function [ x ] when resolve x.t = TStr -> Some(stdCall "rev" [ x ]) | _ -> None) // `-"abc"` is "cba"
            "zip", (function
                | [ a; b ] when resolve a.t = TStr || resolve b.t = TStr ->
                    let chars (x: E) = if resolve x.t = TStr then stdCall "chars" [ x ] else x
                    Some(stdCall "zip" [ chars a; chars b ])
                | _ -> None) ] }
