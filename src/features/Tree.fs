/// Sorted trees (a treap in the runtime): `BT[..]`, `BT:xs`, `BT:xs,{key}` (ordered by a key block, then the
/// element), `t + x` / `t - x` in place, `t@?x`; min/max/pop/arr are runtime stdlib functions.
/// The type is `TCon("BT", [el])`, or `TCon("BT", [el; TFn keyBlock; keyType])` with a key.
module Features.Tree

open Ast
open Core
open Typer

/// `t + x`, `t - x`, `has(t, x)` on a tree: the runtime gets the element and its key (the element itself
/// when the tree has no key block); `+`/`-` change t in place and yield it.
let treeOp f (c: E) (x: E) : E =
    let el, key = match resolve c.t with TCon("BT", [ el; TFn id; kt ]) -> el, Some(id, kt) | TCon(_, el :: _) -> el, None | _ -> failwith "treeOp" 
    let tmp (e: E) = typed (Var(declare cur (Desugar.fresh "t") e.t)) e.t
    let x = own (conv x el)
    let tc, tx = tmp c, tmp x
    let k =
        match key with
        | None -> tx
        | Some(id, kt) ->
            let k = userCall id [ tx ]
            if not (unify kt k.t) then err ($"BT: the key block returns {showT k.t}, not {showT kt}")
            k
    let assign (v: E) (e: E) = let (Var u) = v.k in Assign(u, e)
    let call = stdCall f [ tc; k; tx ]
    let result = if f = "bthas" then call else typed (Seq([ Do call ], tc)) c.t
    typed (Seq([ assign tc c; assign tx x ], result)) result.t

let private isTree t = match resolve t with TCon("BT", _) -> true | _ -> false

/// `BT[..]` / `BT:xs` / `BT:xs,{key}`: an empty tree, then each element added with `+`
let private ctor (args: E list) =
    match args with
    | xs :: f ->
        let key =
            match f with
            | [] -> None
            | [ f ] -> (match resolve f.t with TFn id -> Some(id, newHole ()) | _ -> err "BT: the second argument must be a key block")
            | _ -> err "BT: expects a collection and an optional key block"
        let el = elemOf "BT" xs
        let t = match key with None -> TCon("BT", [ el ]) | Some(id, kt) -> TCon("BT", [ el; TFn id; kt ])
        let tv = typed (Var(declare cur (Desugar.fresh "bt") t)) t
        let fill =
            match xs.k with
            | Arr [] -> []
            | _ ->
                let el = Desugar.fresh "_"
                let it = { kind = IEach; src = xs; el = el; ix = Desugar.fresh "@"; acc = Desugar.fresh "$"; body = [ Do(mk (Bin("+", tv, mk (Var el)))) ]; post = [] }
                [ Do(typeE (mk (Iter it))) ]
        let (Var u) = tv.k
        Some(typed (Seq(Assign(u, typed (Call("BT", [])) t) :: fill, tv)) t)
    | [] -> None

let feature =
    { Feature.empty with
        name = "tree"
        builtins = [ "BT" ]
        lex = [ Coll.literalOpener "BT" ]
        prefix = [ "BT[", Coll.literal "BT" ]
        types =
          [ { con = "BT"
              show = (fun s ts -> match ts with [ e; _; k ] -> "BT " + s e + " by " + s k | _ -> "BT " + s ts.Head)
              desc = fun d ts -> match ts with [ e; _; k ] -> "K" + d k + d e | _ -> "T" + d ts.Head } ]
        prims = [ "BT", ctor; "has", (function [ c; x ] when isTree c.t -> Some(treeOp "bthas" c x) | _ -> None) ]
        bin = [ fun op _ a b -> match op with ("+" | "-") when isTree a.t -> Some(treeOp (if op = "+" then "btadd" else "btdel") a b) | _ -> None ]
        llvm = [ "BT", fun g e _ _ -> g.rt "ptr" "ns_bt_new" [ g.desc e.t ] ] } // empty; the typer adds the elements
