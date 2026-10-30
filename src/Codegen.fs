/// Stage 6: typed core -> textual LLVM IR, following the layouts chosen by Repr. Values are i64/double/i1, ptr or
/// value-tuple structs; the runtime receives generic values as pointers to their storage (see README, Representation).
module Codegen

open System
open System.Text
open System.Collections.Generic
open Core
open Typer

let desc = Repr.desc
let llty = Repr.llty

let private isFn t = match resolve t with TFn _ -> true | _ -> false

/// True when evaluating `e` cannot reassign a variable (no statements, no user calls).
let rec private noAssign (e: E) =
    match e.k with
    | Seq _ | Iter _ | MatchOn _ -> false
    | UserCall _ -> false
    | Call(_, xs) | StdCall(_, xs) | Arr xs -> List.forall noAssign xs
    | Bin(_, a, b) | Index(a, b) -> noAssign a && noAssign b
    | If(a, b, c) -> noAssign a && noAssign b && noAssign c
    | Conv x -> noAssign x
    | _ -> true

type private Ctx =
    { body: StringBuilder
      allocas: StringBuilder
      locals: Dictionary<string, string>
      caps: Set<string>
      mutable tmp: int }

let generate (prog: Program) : string =
    let strings = Dictionary<string, string>()
    let decls = Dictionary<string, string>()
    let globals = prog.globals |> List.map fst |> set
    let instByName = prog.insts |> List.map (fun i -> i.name, i) |> dict
    let mutable c = Unchecked.defaultof<Ctx>
    // top-level variables no function touches live in main's allocas (registers after mem2reg), not in globals
    let fnGlobals = HashSet<string>()
    let mutable inMain = false

    /// String literals are immortal objects {rc=-1, desc=null, bytes}; the operand points at the bytes.
    let strConst (s: string) =
        let n =
            match strings.TryGetValue s with
            | true, n -> n
            | _ ->
                let n = $"@.s{strings.Count}"
                strings.[s] <- n
                n
        let len = Encoding.UTF8.GetByteCount s + 1
        $"getelementptr inbounds ({{ i64, ptr, [{len} x i8] }}, ptr {n}, i32 0, i32 2)"
    let tmp () =
        c.tmp <- c.tmp + 1
        $"%%t{c.tmp}"
    let label () =
        c.tmp <- c.tmp + 1
        $"L{c.tmp}"
    let emit (s: string) = c.body.Append("  ").Append(s).Append('\n') |> ignore
    let place (l: string) = c.body.Append(l).Append(":\n") |> ignore
    let alloca ty =
        let r = tmp ()
        c.allocas.Append($"  {r} = alloca {ty}\n") |> ignore
        r
    let ptrOf (u: string) =
        if globals.Contains u && not (inMain && not (fnGlobals.Contains u)) then
            fnGlobals.Add u |> ignore // functions are emitted before main
            $"@\"g.{u}\""
        elif c.caps.Contains u then $"%%\"c.{u}\""
        else
            match c.locals.TryGetValue u with
            | true, p -> p
            | _ ->
                let ty = llty prog.varTy.[u]
                let p = alloca ty
                c.allocas.Append($"  store {ty} zeroinitializer, ptr {p}\n") |> ignore
                c.locals.[u] <- p
                p
    /// Calls a runtime function; args are (llvm type, operand) pairs.
    let rt (ret: string) (name: string) (args: (string * string) list) =
        let tys = args |> List.map fst |> String.concat ", "
        decls.[name] <- $"declare {ret} @{name}({tys})"
        let argStr = args |> List.map (fun (t, v) -> t + " " + v) |> String.concat ", "
        if ret = "void" then (emit ($"call void @{name}({argStr})"); "")
        else
            let r = tmp ()
            emit ($"{r} = call {ret} @{name}({argStr})")
            r
    let instr (s: string) = let r = tmp () in emit (r + " = " + s); r
    /// Puts a value into a fresh stack cell in its storage layout and returns the cell's address.
    let spill (v: string) t =
        let p = alloca (Repr.storage t)
        let sv = if resolve t = TBool then instr $"zext i1 {v} to i8" else v
        emit ($"store {Repr.storage t} {sv}, ptr {p}")
        p
    /// Reads a value from storage (not retained).
    let loadFrom (p: string) t =
        let v = instr $"load {Repr.storage t}, ptr {p}"
        if resolve t = TBool then instr $"trunc i8 {v} to i1" else v
    /// A cell the runtime writes an owned result into.
    let outCell t = alloca (Repr.storage t)
    /// Writes a value into element storage (layout per Repr).
    let storeTo (v: string) t (p: string) =
        let sv = if resolve t = TBool then instr $"zext i1 {v} to i8" else v
        emit ($"store {Repr.storage t} {sv}, ptr {p}")
    let d t = "ptr", strConst (desc t)
    // Reference counting: every expression yields an owned (+1) value; runtime args are borrowed.
    let isPtr = Repr.isPtr
    let release (v: string) t = if isPtr t then rt "void" "ns_release" [ "ptr", v ] |> ignore
    let retain (v: string) t = if isPtr t then rt "void" "ns_retain" [ "ptr", v ] |> ignore

    // a variable no callee can reassign: not captured by any instance, not a global a function touches
    let allCaps = prog.insts |> Seq.collect (fun i -> i.caps) |> HashSet
    let lendable (u: string) =
        not (allCaps.Contains u) && (not (globals.Contains u) || (inMain && not (fnGlobals.Contains u)))

    let wrappers = StringBuilder()
    let wrapped = HashSet<string>()

    let rec ex (e: E) : string =
        match e.k with
        | Int v -> string v
        | Float f -> $"0x{BitConverter.DoubleToInt64Bits f:X16}"
        | Bool b -> if b then "true" else "false"
        | Str s -> strConst s
        | Var u when isFn e.t -> "0"
        | Var u ->
            let v = instr $"load {llty e.t}, ptr {ptrOf u}"
            retain v e.t
            v
        | Fn _ -> "0"
        | Arr xs ->
            let a = rt "ptr" "ns_arr_new" [ d e.t; "i64", string xs.Length ]
            for x in xs do
                let v = ex x
                storeTo v x.t (rt "ptr" "ns_arr_append" [ "ptr", a ]) // the owned value moves in
            a
        | Bin(op, a, b) -> binop op a b
        | If(cond, a, b) ->
            let res = if isFn e.t then "" else alloca (llty e.t)
            let lt, le, lend = label (), label (), label ()
            emit ($"br i1 {ex cond}, label %%{lt}, label %%{le}")
            for (l, x) in [ lt, a; le, b ] do
                place l
                let v = ex x
                if res <> "" then emit ($"store {llty e.t} {v}, ptr {res}")
                emit ($"br label %%{lend}")
            place lend
            if res = "" then "0" else instr $"load {llty e.t}, ptr {res}"
        | Desc -> strConst (desc e.t)
        | Call(f, args) -> call e f args
        | StdCall(sg, args) -> stdCall e sg args
        | UserCall(f, args) ->
            // user functions own their arguments, except borrowed parameters: those are lent and dropped after
            let inst = instByName.[f]
            let evs =
                List.zip args inst.parms |> List.mapi (fun j (a, (p, _)) ->
                    if inst.borrowed.Contains p && isPtr a.t then
                        match a.k with
                        | Var u when lendable u && List.forall noAssign (List.skip (j + 1) args) -> exB a
                        | _ -> ex a, true
                    else ex a, false)
            let ps = List.zip evs (inst.parms |> List.map snd) |> List.map (fun ((v, _), t) -> llty t + " " + v)
            let cs = inst.caps |> List.map (fun u -> "ptr " + ptrOf u)
            let r = tmp ()
            let argList = ps @ cs |> String.concat ", "
            emit $"{r} = call {llty inst.ret} @\"{inst.name}\"({argList})"
            List.zip evs args |> List.iter (fun (v, a) -> drop v a.t)
            r
        | Index(x, i) ->
            let [ (xv, xo); (iv, io) ] = exArgs 0 [ x; i ]
            let r =
                match resolve x.t with
                | TStr -> rt "ptr" "ns_str_at" [ "ptr", xv; "i64", iv ]
                | TMap(k, _) ->
                    // present: read the value in place (borrowed, so retain); missing: the runtime's zero value
                    let out = outCell e.t
                    let key = spill iv k
                    let p = rt "ptr" "ns_map_ref" [ "ptr", xv; "ptr", key ]
                    let lf, ls, lend = label (), label (), label ()
                    let present = instr $"icmp ne ptr {p}, null"
                    emit $"br i1 {present}, label %%{lf}, label %%{ls}"
                    place lf
                    let v = loadFrom p e.t
                    retain v e.t
                    storeTo v e.t out
                    emit ($"br label %%{lend}")
                    place ls
                    rt "void" "ns_map_get" [ "ptr", xv; "ptr", key; "ptr", out ] |> ignore
                    emit ($"br label %%{lend}")
                    place lend
                    loadFrom out e.t
                | _ ->
                    let v = loadFrom (rt "ptr" "ns_arr_at" [ "ptr", xv; "i64", iv ]) e.t
                    retain v e.t // the element is borrowed from the array
                    v
            drop (xv, xo) x.t
            drop (iv, io) i.t
            r
        | Conv x ->
            let v, owned = exB x
            if resolve x.t = resolve e.t then (if owned then v else (retain v x.t; v))
            else
                let r = convert1 v x.t e.t
                drop (v, owned) x.t
                r
        | Seq(ss, x) ->
            stmts ss
            ex x
        | Iter _ | MatchOn _ -> failwith "Iter/MatchOn must be lowered by the typer"
        | BlockRef _ -> failwith "a block reference is only a stdlib argument"

    /// Evaluates for a borrowing use: a plain variable read skips retain; returns (value, owned).
    and exB (e: E) : string * bool =
        match e.k with
        | Var u when isPtr e.t && not (isFn e.t) -> instr $"load ptr, ptr {ptrOf u}", false
        | _ -> ex e, true

    /// Operands left to right; one is borrowed only if nothing evaluated after it can reassign variables.
    and exArgs (owned: int) (es: E list) =
        es |> List.mapi (fun i e ->
            if i >= owned && List.forall noAssign (List.skip (i + 1) es) then exB e else ex e, true)

    and drop (v: string, owned) t = if owned then release v t

    and convert1 (v: string) (src: Ty) (dst: Ty) =
        match resolve src, resolve dst with
        | TInt, TFloat -> instr $"sitofp i64 {v} to double"
        | TFloat, TInt -> instr $"fptosi double {v} to i64"
        | TInt, TBool -> instr $"icmp ne i64 {v}, 0"
        | TFloat, TBool -> instr $"fcmp une double {v}, 0.0"
        | TBool, TInt -> instr $"zext i1 {v} to i64"
        | TBool, TFloat -> instr $"uitofp i1 {v} to double"
        | TStr, TInt -> rt "i64" "ns_s2i" [ "ptr", v ]
        | TStr, TFloat -> rt "double" "ns_s2f" [ "ptr", v ]
        | s, TStr -> rt "ptr" "ns_tostr" [ d s; "ptr", spill v s ]
        | s, TBool ->
            let n = rt "i64" "ns_len" [ d s; "ptr", spill v s ]
            instr $"icmp ne i64 {n}, 0"
        | s, t -> failwith $"codegen: no conversion {showTy s} -> {showTy t}"

    and binop op (a: E) (b: E) =
        let [ (av, ao); (bv, bo) ] = exArgs 0 [ a; b ]
        let r = binop1 op a b av bv
        drop (av, ao) a.t
        drop (bv, bo) b.t
        r

    and binop1 op (a: E) (b: E) av bv =
        let cmpOp = dict [ "=", ("eq", "oeq", "eq"); "!=", ("ne", "une", "ne"); "<", ("slt", "olt", "ult"); ">", ("sgt", "ogt", "ugt"); "<=", ("sle", "ole", "ule"); ">=", ("sge", "oge", "uge") ]
        match resolve a.t, op with
        | TInt, _ when cmpOp.ContainsKey op -> let (i, _, _) = cmpOp.[op] in instr $"icmp {i} i64 {av}, {bv}"
        | TFloat, _ when cmpOp.ContainsKey op -> let (_, f, _) = cmpOp.[op] in instr $"fcmp {f} double {av}, {bv}"
        | TBool, _ when cmpOp.ContainsKey op -> let (_, _, u) = cmpOp.[op] in instr $"icmp {u} i1 {av}, {bv}"
        | TStr, ("=" | "!=") ->
            let r = rt "i64" "ns_str_eq" [ "ptr", av; "ptr", bv ]
            let pred = if op = "=" then "ne" else "eq"
            instr $"icmp {pred} i64 {r}, 0"
        | TTup ts, ("=" | "!=") when Repr.isValueTuple a.t ->
            // value tuple: compare members in registers (bools are widened to i64)
            let eqs = ts |> List.mapi (fun i m ->
                let x, y = instr $"extractvalue {llty a.t} {av}, {i}", instr $"extractvalue {llty a.t} {bv}, {i}"
                if resolve m = TFloat then instr $"fcmp oeq double {x}, {y}" else instr $"icmp eq i64 {x}, {y}")
            let all = eqs.Tail |> List.fold (fun acc q -> instr $"and i1 {acc}, {q}") eqs.Head
            if op = "=" then all else instr $"xor i1 {all}, true"
        | t, ("=" | "!=") when t <> TStr ->
            let r = rt "i64" "ns_eq" [ d t; "ptr", spill av t; "ptr", spill bv t ]
            let pred = if op = "=" then "ne" else "eq"
            instr $"icmp {pred} i64 {r}, 0"
        | t, _ when cmpOp.ContainsKey op ->
            let r = rt "i64" "ns_cmp" [ d t; "ptr", spill av t; "ptr", spill bv t ]
            let (i, _, _) = cmpOp.[op]
            instr $"icmp {i} i64 {r}, 0"
        | TInt, ("/" | "%" | "^^") ->
            rt "i64" (match op with "/" -> "ns_idiv" | "%" -> "ns_imod" | _ -> "ns_ipow") [ "i64", av; "i64", bv ]
        | TInt, _ ->
            let ins = match op with "+" -> "add" | "-" -> "sub" | "*" -> "mul" | "&" -> "and" | "^" -> "xor" | "<<" -> "shl" | ">>" -> "ashr" | ">>>" -> "lshr" | _ -> "or"
            instr $"{ins} i64 {av}, {bv}"
        | TFloat, "^^" ->
            decls.["llvm.pow.f64"] <- "declare double @llvm.pow.f64(double, double)"
            instr $"call double @llvm.pow.f64(double {av}, double {bv})"
        | TFloat, _ ->
            let ins = match op with "+" -> "fadd" | "-" -> "fsub" | "*" -> "fmul" | "/" -> "fdiv" | _ -> "frem"
            instr $"{ins} double {av}, {bv}"
        | TBool, _ ->
            let ins = match op with "+" -> "or" | "*" -> "and" | _ -> "xor"
            instr $"{ins} i1 {av}, {bv}"
        | TStr, "+" -> rt "ptr" "ns_concat" [ "ptr", av; "ptr", bv ]
        | TStr, "*" -> rt "ptr" "ns_repeat" [ "ptr", av; "i64", bv ]
        | t, _ -> failwith $"codegen: operator {op} on {showTy t}"

    and call (e: E) (f: string) (args: E list) =
        match f, args with
        | "mapof", [ { k = Arr ks }; { k = Arr vs } ] ->
            let m = rt "ptr" "ns_map_new" [ d e.t ]
            for (k, v) in List.zip ks vs do
                let kv, vv = ex k, ex v
                rt "void" "ns_map_set" [ "ptr", m; "ptr", spill kv k.t; "ptr", spill vv v.t ] |> ignore
                release kv k.t
                release vv v.t
            m
        | _ ->
        // pass-through results own their first argument
        let kept =
            match f with
            | "print" | "println" | "push" | "add" | "del" | "merge" -> 1
            | _ -> 0
        let evs = exArgs kept args
        let r = call1 e f args (List.map fst evs)
        List.zip evs args |> List.skip kept |> List.iter (fun (v, a) -> drop v a.t)
        r

    and call1 (e: E) (f: string) (args: E list) (vs: string list) =
        let arg i = vs.[i]
        let ty i = args.[i].t
        let cell i = "ptr", spill (arg i) (ty i)
        match f, args with
        | ("print" | "println"), [ x ] -> (rt "void" ("ns_" + f) [ d x.t; cell 0 ] |> ignore; arg 0)
        | "neg", [ x ] -> if resolve x.t = TFloat then instr $"fneg double {arg 0}" else instr $"sub i64 0, {arg 0}"
        | "not", _ -> instr $"xor i1 {arg 0}, true"
        | "len", [ x ] -> rt "i64" "ns_len" [ d x.t; cell 0 ]
        | "has", [ x; _ ] ->
            let r = rt "i64" "ns_has" [ d x.t; "ptr", arg 0; cell 1 ]
            instr $"icmp ne i64 {r}, 0"
        | "range", _ -> rt "ptr" "ns_range" [ "i64", arg 0; "i64", arg 1 ]
        | "slice", [ x; _; _ ] ->
            rt "ptr" (if resolve x.t = TStr then "ns_slice_s" else "ns_arr_slice") [ "ptr", arg 0; "i64", arg 1; "i64", arg 2 ]
        | "BT", [] -> rt "ptr" "ns_bt_new" [ d e.t ] // empty; the typer adds the elements
        | ("set" | "Q" | "PQ"), [ xs ] ->
            let ctor = match f with "set" -> "ns_set_new" | "Q" -> "ns_q_new" | _ -> "ns_pq_new"
            let coll = rt "ptr" ctor [ d e.t ]
            rt "void" "ns_add_all" [ d e.t; "ptr", coll; "ptr", arg 0 ] |> ignore
            coll
        | "peek", [ q ] ->
            let out = outCell e.t
            rt "void" "ns_peek" [ d q.t; "ptr", arg 0; "ptr", out ] |> ignore
            loadFrom out e.t
        | "tuple", _ when Repr.isValueTuple e.t ->
            // value tuple: an LLVM struct built in registers (bool members widened to i64)
            args |> List.indexed |> List.fold (fun acc (i, a) ->
                let m = if resolve a.t = TBool then instr $"zext i1 {arg i} to i64" else arg i
                let mt = if resolve a.t = TFloat then "double" else "i64"
                instr $"insertvalue {llty e.t} {acc}, {mt} {m}, {i}") "undef"
        | "tuple", _ ->
            let t = rt "ptr" "ns_tup_new" [ d e.t ]
            args |> List.iteri (fun i a -> rt "void" "ns_tup_set" [ "ptr", t; "i64", string i; cell i ] |> ignore)
            t
        | "tget", [ x; { k = Int k } ] when Repr.isValueTuple x.t ->
            let m = instr $"extractvalue {llty x.t} {arg 0}, {k}"
            if resolve e.t = TBool then instr $"icmp ne i64 {m}, 0" else m
        | "tget", [ x; { k = Int k } ] ->
            // boxed tuple: the member sits at a fixed offset in the object; borrowed from it, so retain
            let p = instr $"getelementptr i8, ptr {arg 0}, i64 {Repr.tupOffset x.t (int k)}"
            // members are packed, so one after a bool member may be unaligned
            let v = instr $"load {Repr.storage e.t}, ptr {p}, align 1"
            let v = if resolve e.t = TBool then instr $"trunc i8 {v} to i1" else v
            retain v e.t
            v
        | "repeat", [ x; _ ] -> rt "ptr" "ns_arr_repeat" [ d e.t; cell 0; "i64", arg 1 ]
        | "panic", _ -> (rt "void" "ns_panic" [ "ptr", arg 0 ] |> ignore; "0")
        | "try", _ ->
            // `?? stmt`: a handler frame in this function; _setjmp returns again (nonzero) when the statement panics
            let buf = alloca "[512 x i8], align 16"
            rt "void" "ns_try" [ "ptr", buf ] |> ignore
            // Win64's _setjmp takes a frame to unwind to (null: plain longjmp); other ABIs ignore the extra argument
            decls.["_setjmp"] <- "declare i32 @_setjmp(ptr, ptr) returns_twice"
            let r = instr $"call i32 @_setjmp(ptr {buf}, ptr null) returns_twice"
            instr $"icmp eq i32 {r}, 0"
        | "untry", _ -> (rt "void" "ns_untry" [] |> ignore; "0")
        | "copy", _ -> rt "ptr" "ns_arr_copy" [ "ptr", arg 0 ]
        | "push", _ ->
            storeTo (arg 1) (ty 1) (rt "ptr" "ns_arr_append" [ "ptr", arg 0 ])
            retain (arg 1) (ty 1) // the array holds its own reference; the argument is dropped as usual
            arg 0
        | "append", _ ->
            let cp = rt "ptr" "ns_arr_copy" [ "ptr", arg 0 ]
            storeTo (arg 1) (ty 1) (rt "ptr" "ns_arr_append" [ "ptr", cp ])
            retain (arg 1) (ty 1)
            cp
        | "concat", _ -> rt "ptr" "ns_arr_concat" [ "ptr", arg 0; "ptr", arg 1 ]
        | "add", [ coll; _ ] -> (rt "void" "ns_add" [ d coll.t; "ptr", arg 0; cell 1 ] |> ignore; arg 0)
        | "del", [ coll; _ ] -> (rt "void" "ns_del" [ d coll.t; "ptr", arg 0; cell 1 ] |> ignore; arg 0)
        | "merge", _ -> (rt "void" "ns_map_merge" [ "ptr", arg 0; "ptr", arg 1 ] |> ignore; arg 0)
        | _ -> failwith $"codegen: unknown call {f}/{args.Length}"

    /// Calls a runtime stdlib function per its signature: descriptors, then arguments (type-variable values by
    /// storage pointer, bools as i64, a block as callback + env), then an out cell for a type-variable result.
    /// Arguments are borrowed by C, so released afterwards.
    and stdCall (e: E) (sg: Stdlib.Sig) (args: E list) =
        let nd = args.Length - sg.parms.Length // descriptors
        let descs = args |> List.take nd |> List.map (fun a -> "ptr", ex a)
        let mutable owned = []
        let cargs =
            List.map2 (fun (p: Stdlib.STy) (a: E) ->
                match p, a.k with
                | Stdlib.SFn _, BlockRef b ->
                    let inst = instByName.[b]
                    [ "ptr", wrapper inst; "ptr", envOf inst ]
                | _ ->
                    let v = ex a
                    owned <- (v, a.t) :: owned
                    match p with
                    | Stdlib.SVar _ | Stdlib.STup _ -> [ "ptr", spill v a.t ]
                    | Stdlib.SBool -> [ "i64", instr $"zext i1 {v} to i64" ]
                    | _ -> [ llty a.t, v ]) sg.parms (List.skip nd args)
            |> List.concat
        let r =
            match sg.ret with
            | Stdlib.SVar _ | Stdlib.STup _ ->
                let out = outCell e.t
                rt "void" sg.csym (descs @ cargs @ [ "ptr", out ]) |> ignore
                loadFrom out e.t
            | Stdlib.SBool ->
                let b = rt "i64" sg.csym (descs @ cargs)
                instr $"icmp ne i64 {b}, 0"
            | _ -> rt (llty e.t) sg.csym (descs @ cargs)
        for (v, t) in owned do release v t
        r

    /// `void w.<inst>(ptr env, ptr x, ptr out)`: a C-callable entry for a block instance; env holds its capture
    /// pointers, x points at the (borrowed) argument's storage, the owned result is stored to out.
    and wrapper (inst: Inst) : string =
        if wrapped.Add inst.name then
            let saved = c
            c <- { body = StringBuilder(); allocas = StringBuilder(); locals = Dictionary(); caps = Set.empty; tmp = 0 }
            let caps = inst.caps |> List.mapi (fun i _ -> let slot = instr $"getelementptr ptr, ptr %%env, i64 {i}" in instr $"load ptr, ptr {slot}")
            // one parameter: x points at it; several: x is an array of pointers to them
            let args =
                inst.parms |> List.mapi (fun i (u, pt) ->
                    let p =
                        if inst.parms.Length = 1 then "%x"
                        else let slot = instr $"getelementptr ptr, ptr %%x, i64 {i}" in instr $"load ptr, ptr {slot}"
                    let a = loadFrom p pt
                    if not (inst.borrowed.Contains u) then retain a pt // the instance owns its parameter; C passes a borrowed element
                    llty pt + " " + a)
            let r = tmp ()
            let ps = args @ (caps |> List.map (fun p -> "ptr " + p))
            let argList = String.concat ", " ps
            emit $"{r} = call {llty inst.ret} @\"{inst.name}\"({argList})"
            let sr = if resolve inst.ret = TBool then instr $"zext i1 {r} to i8" else r
            emit ($"store {Repr.storage inst.ret} {sr}, ptr %%out")
            emit "ret void"
            wrappers.Append($"define void @\"w.{inst.name}\"(ptr %%env, ptr %%x, ptr %%out) {{\nentry:\n{string c.allocas}{string c.body}}}\n\n") |> ignore
            c <- saved
        $"@\"w.{inst.name}\""

    and envOf (inst: Inst) : string =
        if inst.caps.IsEmpty then "null"
        else
            let env = alloca ($"[{inst.caps.Length} x ptr]")
            inst.caps |> List.iteri (fun i u ->
                let slot = instr $"getelementptr ptr, ptr {env}, i64 {i}"
                emit $"store ptr {ptrOf u}, ptr {slot}")
            env

    and stmt (s: S) =
        match s with
        | Line _ -> ()
        | Do e -> release (ex e) e.t
        | Assign(u, e) when isFn prog.varTy.[u] -> ()
        | Assign(u, e) ->
            let v = ex e
            let p = ptrOf u
            let old = if isPtr e.t then instr $"load ptr, ptr {p}" else ""
            emit ($"store {llty e.t} {v}, ptr {p}")
            release old e.t
        | SetIdx(x, i, v) ->
            let [ (xv, xo); (iv, io); (vv, vo) ] = exArgs 0 [ x; i; v ]
            // locate the element's storage, then replace it in place: retain the new value before releasing the old
            let p =
                match resolve x.t with
                | TMap(k, _) -> rt "ptr" "ns_map_put" [ "ptr", xv; "ptr", spill iv k ]
                | _ -> rt "ptr" "ns_arr_at" [ "ptr", xv; "i64", iv ]
            let old = if isPtr v.t then instr $"load ptr, ptr {p}" else ""
            retain vv v.t
            storeTo vv v.t p
            release old v.t
            drop (xv, xo) x.t
            drop (iv, io) i.t
            drop (vv, vo) v.t
        | While(cond, body) ->
            let lc, lb, lend = label (), label (), label ()
            emit ($"br label %%{lc}")
            place lc
            emit ($"br i1 {ex cond}, label %%{lb}, label %%{lend}")
            place lb
            stmts body
            emit ($"br label %%{lc}")
            place lend

    and stmts ss = List.iter stmt ss

    let newCtx caps = { body = StringBuilder(); allocas = StringBuilder(); locals = Dictionary(); caps = caps; tmp = 0 }
    let out = StringBuilder()
    let fns = StringBuilder()

    for inst in prog.insts do
        c <- newCtx (set inst.caps)
        for (u, t) in inst.parms do emit ($"store {llty t} %%\"p.{u}\", ptr {ptrOf u}")
        stmts inst.body
        let r = ex inst.result
        for KeyValue(u, p) in List.ofSeq c.locals do
            if isPtr prog.varTy.[u] && not (inst.borrowed.Contains u) then release (instr $"load ptr, ptr {p}") prog.varTy.[u]
        emit ($"ret {llty inst.ret} {r}")
        let ps = (inst.parms |> List.map (fun (u, t) -> $"{llty t} %%\"p.{u}\"")) @ (inst.caps |> List.map (fun u -> $"ptr %%\"c.{u}\""))
        let parmList = String.concat ", " ps
        fns.Append($"define {llty inst.ret} @\"{inst.name}\"({parmList}) {{\nentry:\n{c.allocas}{c.body}}}\n\n") |> ignore

    c <- newCtx Set.empty
    inMain <- true
    stmts prog.main
    for KeyValue(u, p) in List.ofSeq c.locals do // main's former globals: release like any function's locals
        if isPtr prog.varTy.[u] then release (instr $"load ptr, ptr {p}") prog.varTy.[u]
    emit "ret i32 0"
    fns.Append($"define i32 @main() {{\nentry:\n{string c.allocas}{string c.body}}}\n") |> ignore

    for (g, t) in prog.globals do
        if not (isFn t) then
            let init = if llty t = "ptr" then "null" else "zeroinitializer"
            out.Append($"@\"g.{g}\" = internal global {llty t} {init}\n") |> ignore
    for KeyValue(s, n) in strings do
        let bytes = Encoding.UTF8.GetBytes s
        let esc = bytes |> Array.map (fun b -> if b >= 32uy && b < 127uy && b <> 34uy && b <> 92uy then string (char b) else $"\\{b:X2}") |> String.concat ""
        out.Append($"{n} = private unnamed_addr constant {{ i64, ptr, [{bytes.Length + 1} x i8] }} {{ i64 -1, ptr null, [{bytes.Length + 1} x i8] c\"{esc}\\00\" }}\n") |> ignore
    for KeyValue(_, decl) in decls do out.Append(decl).Append('\n') |> ignore
    out.Append('\n').Append(fns).Append(wrappers).ToString()
