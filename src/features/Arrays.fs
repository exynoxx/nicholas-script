/// Array operations beyond literals and indexing (which are the kernel's): `xs ++ x` / `xs ++ ys`, `xs ** n`
/// repetition, elementwise arithmetic (`xs + 1`, `xs * ys`, `-xs`), `push`, `[x]**n` (`repeat`), and `x = x ++ v`
/// as an in-place push.
module Features.Arrays

open Ast
open Core
open Typer

let private v n = mk (Var n)
let private call f args = mk (Call(f, args))
let private bin op a b = mk (Bin(op, a, b))
let private seq ss e = mk (Seq(ss, e))

/// `a op b` with an array on the left: elementwise, the right side either a scalar or an array.
let broadcast op (a: E) (b: E) (bIsArr: bool) =
    let y = Desugar.fresh "y"
    let el, ix, acc = Desugar.fresh "_", Desugar.fresh "@", Desugar.fresh "$"
    let rhs = if bIsArr then mk (Index(v y, v ix)) else v y
    let map = mk (Iter { kind = IMap; src = a; el = el; ix = ix; acc = acc; body = [ Do(bin op (v el) rhs) ]; post = [] })
    if not bIsArr then seq [ Assign(y, b) ] map
    else // a longer `a` fails indexing `y`; a shorter one is caught after the loop (checked after, so `a` still fuses)
        let r = Desugar.fresh "r"
        let mismatch = mk (If(bin "!=" (call "len" [ v r ]) (call "len" [ v y ]), seq [ Do(call "panic" [ mk (Str "array length mismatch") ]) ] (mk (Int 0L)), mk (Int 0L)))
        seq [ Assign(y, b); Assign(r, map); Do mismatch ] (v r)

let private bins op (_: bool) (a: E) (b: E) =
    let isArith = List.contains op [ "+"; "-"; "*"; "/"; "%"; "^^" ]
    let isBit = List.contains op [ "^"; "<<"; ">>"; ">>>" ]
    let L = resolve a.t
    match op, L with
    | "++", TArr el ->
        match resolve b.t with
        | TArr _ -> Some(typed (Call("concat", [ a; conv b L ])) L)
        | TCon(_, [ e ]) when not (unify b.t el) && unify e el -> Some(typed (Call("concat", [ a; stdCall "arr" [ b ] ])) L)
        | _ -> Some(typed (Call("append", [ a; own (conv b el) ])) L)
    // `**` repeats: `[2,3]**2` = [2,3,2,3] (`[x]**n` becomes `repeat` in Desugar)
    | "**", TArr _ -> Some(stdCall "flatten" [ builtin "repeat" [ a; b ] ])
    | _, TArr _ when isArith || isBit ->
        let bIsArr = match resolve b.t with TArr _ -> true | _ -> false
        let rec depth t = match resolve t with TArr e -> 1 + depth e | _ -> 0
        if bIsArr && depth L < depth b.t then
            err $"{showT L} {op} {showT b.t}: arrays combine element by element, and the right one is nested deeper"
        Some(typeE (broadcast op a b bIsArr))
    | _ -> None

let private isArr (x: E) = match resolve x.t with TArr _ -> true | _ -> false

let feature =
    { Feature.empty with
        name = "arrays"
        bin = [ bins ]
        index = [ fun x i -> match resolve x.t with TArr el -> Some(typed (Index(x, conv i TInt)) el) | _ -> None ]
        setIndex = [ fun x i v -> match resolve x.t with TArr el -> Some(SetIdx(x, conv i TInt, own (conv v el))) | _ -> None ]
        // a literal: a new array, each element's owned value moved in
        llvmNode =
          [ fun g e ->
                match e.k with
                | Arr xs ->
                    let a = g.rt "ptr" "ns_arr_new" [ g.desc e.t; "i64", string xs.Length ]
                    for x in xs do
                        let v = g.ex x
                        g.storeTo v x.t (g.rt "ptr" "ns_arr_append" [ "ptr", a ])
                    Some a
                | _ -> None ]
        llvmIndex =
          [ fun g e x xv iv ->
                if not (isArr x) then None
                else
                    let v = g.loadFrom (g.rt "ptr" "ns_arr_at" [ "ptr", xv; "i64", iv ]) e.t
                    g.retain v e.t // the element is borrowed from the array
                    Some v ]
        llvmSlot = [ fun g t xv iv -> match resolve t with TArr _ -> Some(g.rt "ptr" "ns_arr_at" [ "ptr", xv; "i64", iv ]) | _ -> None ]
        prims =
          [ "neg", (function [ x ] when (match resolve x.t with TArr _ -> true | _ -> false) -> Some(typeE (broadcast "*" x (typed (Int -1L) TInt) false)) | _ -> None)
            "push", (function
                | [ a; x ] ->
                    match resolve a.t with
                    | TArr el -> Some(typed (Call("push", [ a; own (conv (stored x) el) ])) a.t)
                    | _ -> err $"push: not an array: {showVal a}"
                | _ -> None)
            "repeat", (function [ x; n ] -> Some(typed (Call("repeat", [ own (value x); conv n TInt ])) (TArr x.t)) | _ -> None) ]
        // `x = x ++ v`: push onto x itself
        assignRewrite = [ fun u e -> match e.k with Call("append", [ { k = Var src } as a; x ]) when src = u -> Some(Do(typed (Call("push", [ a; x ])) e.t)) | _ -> None ]
        effects = [ "append", Feature.Total; "concat", Feature.Total; "repeat", Feature.Partial; "push", Feature.Mutating ]
        kept = [ "push", 1 ]
        llvm =
          [ "repeat", fun g e args vs -> g.rt "ptr" "ns_arr_repeat" [ g.desc e.t; "ptr", g.spill vs.[0] args.[0].t; "i64", vs.[1] ]
            "push", fun g _ args vs ->
                g.storeTo vs.[1] args.[1].t (g.rt "ptr" "ns_arr_append" [ "ptr", vs.[0] ])
                g.retain vs.[1] args.[1].t // the array holds its own reference; the argument is dropped as usual
                vs.[0]
            "append", fun g _ args vs ->
                let cp = g.rt "ptr" "ns_arr_copy" [ "ptr", vs.[0] ]
                g.storeTo vs.[1] args.[1].t (g.rt "ptr" "ns_arr_append" [ "ptr", cp ])
                g.retain vs.[1] args.[1].t
                cp
            "concat", fun g _ _ vs -> g.rt "ptr" "ns_arr_concat" [ "ptr", vs.[0]; "ptr", vs.[1] ] ] }
