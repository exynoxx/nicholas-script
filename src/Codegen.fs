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
    | Seq _ | Iter _ -> false
    | Call(f, _) when f.StartsWith "@" -> false
    | Call(_, xs) | Arr xs -> List.forall noAssign xs
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
                let n = sprintf "@.s%d" strings.Count
                strings.[s] <- n
                n
        let len = Encoding.UTF8.GetByteCount s + 1
        sprintf "getelementptr inbounds ({ i64, ptr, [%d x i8] }, ptr %s, i32 0, i32 2)" len n
    let tmp () =
        c.tmp <- c.tmp + 1
        sprintf "%%t%d" c.tmp
    let label () =
        c.tmp <- c.tmp + 1
        sprintf "L%d" c.tmp
    let emit (s: string) = c.body.Append("  ").Append(s).Append('\n') |> ignore
    let place (l: string) = c.body.Append(l).Append(":\n") |> ignore
    let alloca ty =
        let r = tmp ()
        c.allocas.Append(sprintf "  %s = alloca %s\n" r ty) |> ignore
        r
    let ptrOf (u: string) =
        if globals.Contains u && not (inMain && not (fnGlobals.Contains u)) then
            fnGlobals.Add u |> ignore // functions are emitted before main
            sprintf "@\"g.%s\"" u
        elif c.caps.Contains u then sprintf "%%\"c.%s\"" u
        else
            match c.locals.TryGetValue u with
            | true, p -> p
            | _ ->
                let ty = llty prog.varTy.[u]
                let p = alloca ty
                c.allocas.Append(sprintf "  store %s zeroinitializer, ptr %s\n" ty p) |> ignore
                c.locals.[u] <- p
                p
    /// Calls a runtime function; args are (llvm type, operand) pairs.
    let rt (ret: string) (name: string) (args: (string * string) list) =
        decls.[name] <- sprintf "declare %s @%s(%s)" ret name (args |> List.map fst |> String.concat ", ")
        let argStr = args |> List.map (fun (t, v) -> t + " " + v) |> String.concat ", "
        if ret = "void" then (emit (sprintf "call void @%s(%s)" name argStr); "")
        else
            let r = tmp ()
            emit (sprintf "%s = call %s @%s(%s)" r ret name argStr)
            r
    let instr fmt = Printf.kprintf (fun s -> let r = tmp () in emit (r + " = " + s); r) fmt
    /// Puts a value into a fresh stack cell in its storage layout and returns the cell's address.
    let spill (v: string) t =
        let p = alloca (Repr.storage t)
        let sv = if resolve t = TBool then instr "zext i1 %s to i8" v else v
        emit (sprintf "store %s %s, ptr %s" (Repr.storage t) sv p)
        p
    /// Reads a value from storage (not retained).
    let loadFrom (p: string) t =
        let v = instr "load %s, ptr %s" (Repr.storage t) p
        if resolve t = TBool then instr "trunc i8 %s to i1" v else v
    /// A cell the runtime writes an owned result into.
    let outCell t = alloca (Repr.storage t)
    /// Writes a value into element storage (layout per Repr).
    let storeTo (v: string) t (p: string) =
        let sv = if resolve t = TBool then instr "zext i1 %s to i8" v else v
        emit (sprintf "store %s %s, ptr %s" (Repr.storage t) sv p)
    let d t = "ptr", strConst (desc t)
    // Reference counting: every expression yields an owned (+1) value; runtime args are borrowed.
    let isPtr = Repr.isPtr
    let release (v: string) t = if isPtr t then rt "void" "ns_release" [ "ptr", v ] |> ignore
    let retain (v: string) t = if isPtr t then rt "void" "ns_retain" [ "ptr", v ] |> ignore

    let wrappers = StringBuilder()
    let wrapped = HashSet<string>()

    let rec ex (e: E) : string =
        match e.k with
        | Int v -> string v
        | Float f -> sprintf "0x%016X" (BitConverter.DoubleToInt64Bits f)
        | Bool b -> if b then "true" else "false"
        | Str s -> strConst s
        | Var u when isFn e.t -> "0"
        | Var u ->
            let v = instr "load %s, ptr %s" (llty e.t) (ptrOf u)
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
            emit (sprintf "br i1 %s, label %%%s, label %%%s" (ex cond) lt le)
            for (l, x) in [ lt, a; le, b ] do
                place l
                let v = ex x
                if res <> "" then emit (sprintf "store %s %s, ptr %s" (llty e.t) v res)
                emit (sprintf "br label %%%s" lend)
            place lend
            if res = "" then "0" else instr "load %s, ptr %s" (llty e.t) res
        | Call("$desc", []) -> strConst (desc e.t)
        | Call(f, args) -> call e f args
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
                    emit (sprintf "br i1 %s, label %%%s, label %%%s" (instr "icmp ne ptr %s, null" p) lf ls)
                    place lf
                    let v = loadFrom p e.t
                    retain v e.t
                    storeTo v e.t out
                    emit (sprintf "br label %%%s" lend)
                    place ls
                    rt "void" "ns_map_get" [ "ptr", xv; "ptr", key; "ptr", out ] |> ignore
                    emit (sprintf "br label %%%s" lend)
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
        | Iter _ -> failwith "Iter must be lowered by the typer"

    /// Evaluates for a borrowing use: a plain variable read skips retain; returns (value, owned).
    and exB (e: E) : string * bool =
        match e.k with
        | Var u when isPtr e.t && not (isFn e.t) -> instr "load ptr, ptr %s" (ptrOf u), false
        | _ -> ex e, true

    /// Operands left to right; one is borrowed only if nothing evaluated after it can reassign variables.
    and exArgs (owned: int) (es: E list) =
        es |> List.mapi (fun i e ->
            if i >= owned && List.forall noAssign (List.skip (i + 1) es) then exB e else ex e, true)

    and drop (v: string, owned) t = if owned then release v t

    and convert (v: string) (src: Ty) (dst: Ty) =
        if resolve src = resolve dst then v
        else
            let r = convert1 v src dst
            release v src
            r

    and convert1 (v: string) (src: Ty) (dst: Ty) =
        match resolve src, resolve dst with
        | TInt, TFloat -> instr "sitofp i64 %s to double" v
        | TFloat, TInt -> instr "fptosi double %s to i64" v
        | TInt, TBool -> instr "icmp ne i64 %s, 0" v
        | TFloat, TBool -> instr "fcmp une double %s, 0.0" v
        | TBool, TInt -> instr "zext i1 %s to i64" v
        | TBool, TFloat -> instr "uitofp i1 %s to double" v
        | TStr, TInt -> rt "i64" "ns_s2i" [ "ptr", v ]
        | TStr, TFloat -> rt "double" "ns_s2f" [ "ptr", v ]
        | s, TStr -> rt "ptr" "ns_tostr" [ d s; "ptr", spill v s ]
        | s, TBool -> instr "icmp ne i64 %s, 0" (rt "i64" "ns_len" [ d s; "ptr", spill v s ])
        | s, t -> failwithf "codegen: no conversion %s -> %s" (showTy s) (showTy t)

    and binop op (a: E) (b: E) =
        let [ (av, ao); (bv, bo) ] = exArgs 0 [ a; b ]
        let r = binop1 op a b av bv
        drop (av, ao) a.t
        drop (bv, bo) b.t
        r

    and binop1 op (a: E) (b: E) av bv =
        let cmpOp = dict [ "=", ("eq", "oeq", "eq"); "~=", ("ne", "une", "ne"); "<", ("slt", "olt", "ult"); ">", ("sgt", "ogt", "ugt"); "<=", ("sle", "ole", "ule"); ">=", ("sge", "oge", "uge") ]
        match resolve a.t, op with
        | TInt, _ when cmpOp.ContainsKey op -> let (i, _, _) = cmpOp.[op] in instr "icmp %s i64 %s, %s" i av bv
        | TFloat, _ when cmpOp.ContainsKey op -> let (_, f, _) = cmpOp.[op] in instr "fcmp %s double %s, %s" f av bv
        | TBool, _ when cmpOp.ContainsKey op -> let (_, _, u) = cmpOp.[op] in instr "icmp %s i1 %s, %s" u av bv
        | t, ("=" | "~=") when t <> TStr ->
            let r = rt "i64" "ns_eq" [ d t; "ptr", spill av t; "ptr", spill bv t ]
            instr "icmp %s i64 %s, 0" (if op = "=" then "ne" else "eq") r
        | t, _ when cmpOp.ContainsKey op ->
            let r = rt "i64" "ns_cmp" [ d t; "ptr", spill av t; "ptr", spill bv t ]
            let (i, _, _) = cmpOp.[op]
            instr "icmp %s i64 %s, 0" i r
        | TInt, ("/" | "%" | "^^") ->
            rt "i64" (match op with "/" -> "ns_idiv" | "%" -> "ns_imod" | _ -> "ns_ipow") [ "i64", av; "i64", bv ]
        | TInt, _ ->
            let ins = match op with "+" -> "add" | "-" -> "sub" | "*" -> "mul" | "&" -> "and" | "^" -> "xor" | "<<" -> "shl" | ">>" -> "ashr" | ">>>" -> "lshr" | _ -> "or"
            instr "%s i64 %s, %s" ins av bv
        | TFloat, "^^" ->
            decls.["llvm.pow.f64"] <- "declare double @llvm.pow.f64(double, double)"
            instr "call double @llvm.pow.f64(double %s, double %s)" av bv
        | TFloat, _ -> instr "%s double %s, %s" (match op with "+" -> "fadd" | "-" -> "fsub" | "*" -> "fmul" | "/" -> "fdiv" | _ -> "frem") av bv
        | TBool, _ -> instr "%s i1 %s, %s" (match op with "+" -> "or" | "*" -> "and" | _ -> "xor") av bv
        | TStr, "+" -> rt "ptr" "ns_concat" [ "ptr", av; "ptr", bv ]
        | TStr, "*" -> rt "ptr" "ns_repeat" [ "ptr", av; "i64", bv ]
        | t, _ -> failwithf "codegen: operator %s on %s" op (showTy t)

    and call (e: E) (f: string) (args: E list) =
        match f, args with
        | f, _ when f.StartsWith "$std:" -> stdCall e (Stdlib.bySym.[f.Substring 5]) args
        | "mapof", [ { k = Arr ks }; { k = Arr vs } ] ->
            let m = rt "ptr" "ns_map_new" [ d e.t ]
            for (k, v) in List.zip ks vs do
                let kv, vv = ex k, ex v
                rt "void" "ns_map_set" [ "ptr", m; "ptr", spill kv k.t; "ptr", spill vv v.t ] |> ignore
                release kv k.t
                release vv v.t
            m
        | _ ->
        // pass-through results own their first argument; user functions own all their arguments
        let kept =
            match f with
            | "print" | "push" | "add" | "del" | "merge" -> 1
            | _ when f.StartsWith "@" -> args.Length
            | _ -> 0
        let evs = exArgs kept args
        let r = call1 e f args (List.map fst evs)
        List.zip evs args |> List.skip kept |> List.iter (fun (v, a) -> drop v a.t)
        r

    and call1 (e: E) (f: string) (args: E list) (vs: string list) =
        let arg i = vs.[i]
        let ty i = args.[i].t
        let cell i = "ptr", spill (arg i) (ty i)
        let elemDesc t = match resolve t with TArr el -> d el | _ -> d TInt
        match f, args with
        | "print", [ x ] -> (rt "void" "ns_print" [ d x.t; cell 0 ] |> ignore; arg 0)
        | "neg", [ x ] -> if resolve x.t = TFloat then instr "fneg double %s" (arg 0) else instr "sub i64 0, %s" (arg 0)
        | "not", _ -> instr "xor i1 %s, true" (arg 0)
        | "len", [ x ] -> rt "i64" "ns_len" [ d x.t; cell 0 ]
        | "has", [ x; _ ] -> instr "icmp ne i64 %s, 0" (rt "i64" "ns_has" [ d x.t; "ptr", arg 0; cell 1 ])
        | "range", _ -> rt "ptr" "ns_range" [ "i64", arg 0; "i64", arg 1 ]
        | "slice", [ x; _; _ ] ->
            rt "ptr" (if resolve x.t = TStr then "ns_slice_s" else "ns_arr_slice") [ "ptr", arg 0; "i64", arg 1; "i64", arg 2 ]
        | ("set" | "Q" | "PQ"), [ xs ] ->
            let el = match resolve e.t with TSet el | TQ el | TPQ el -> el | _ -> TInt
            let ctor = match f with "set" -> "ns_set_new" | "Q" -> "ns_q_new" | _ -> "ns_pq_new"
            let coll = rt "ptr" ctor [ d e.t ]
            rt "void" "ns_add_all" [ d e.t; "ptr", coll; "ptr", arg 0 ] |> ignore
            coll
        | ("pop" | "peek"), [ q ] ->
            let out = outCell e.t
            rt "void" ("ns_" + f) [ d q.t; "ptr", arg 0; "ptr", out ] |> ignore
            loadFrom out e.t
        | ("keys" | "arr"), [ x ] -> rt "ptr" "ns_toarr" [ d x.t; "ptr", arg 0 ]
        | "vals", _ -> rt "ptr" "ns_map_vals" [ "ptr", arg 0 ]
        | "chars", _ -> rt "ptr" "ns_chars" [ "ptr", arg 0 ]
        | "tuple", _ when Repr.isValueTuple e.t ->
            // value tuple: an LLVM struct built in registers (bool members widened to i64)
            args |> List.indexed |> List.fold (fun acc (i, a) ->
                let m = if resolve a.t = TBool then instr "zext i1 %s to i64" (arg i) else arg i
                instr "insertvalue %s %s, %s %s, %d" (llty e.t) acc (if resolve a.t = TFloat then "double" else "i64") m i) "undef"
        | "tuple", _ ->
            let t = rt "ptr" "ns_tup_new" [ d e.t ]
            args |> List.iteri (fun i a -> rt "void" "ns_tup_set" [ "ptr", t; "i64", string i; cell i ] |> ignore)
            t
        | "tget", [ x; { k = Int k } ] when Repr.isValueTuple x.t ->
            let m = instr "extractvalue %s %s, %d" (llty x.t) (arg 0) k
            if resolve e.t = TBool then instr "icmp ne i64 %s, 0" m else m
        | "tget", [ _; { k = Int k } ] ->
            let out = outCell e.t
            rt "void" "ns_tup_get" [ "ptr", arg 0; "i64", string k; "ptr", out ] |> ignore
            loadFrom out e.t
        | "repeat", [ x; _ ] -> rt "ptr" "ns_arr_repeat" [ d e.t; cell 0; "i64", arg 1 ]
        | "panic", _ -> (rt "void" "ns_panic" [ "ptr", arg 0 ] |> ignore; "0")
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
        | name, _ when name.StartsWith "@" ->
            let inst = instByName.[name.Substring 1]
            let ps = List.zip vs (inst.parms |> List.map snd) |> List.map (fun (v, t) -> llty t + " " + v)
            let cs = inst.caps |> List.map (fun u -> "ptr " + ptrOf u)
            let r = tmp ()
            emit (sprintf "%s = call %s @\"%s\"(%s)" r (llty inst.ret) inst.name (ps @ cs |> String.concat ", "))
            r
        | _ -> failwithf "codegen: unknown call %s/%d" f args.Length

    /// Calls a runtime stdlib function per its signature: descriptors, then arguments (type-variable values by
    /// storage pointer, bools as i64, a block as callback + env), then an out cell for a type-variable result.
    /// Arguments are borrowed by C, so released afterwards.
    and stdCall (e: E) (sg: Stdlib.Sig) (args: E list) =
        let nd = sg.tvars.Length
        let descs = args |> List.take nd |> List.map (fun a -> "ptr", ex a)
        let mutable owned = []
        let cargs =
            List.map2 (fun (p: Stdlib.STy) (a: E) ->
                match p, a.k with
                | Stdlib.SFn _, Call(b, []) when b.StartsWith "$block:" ->
                    let inst = instByName.[b.Substring 7]
                    [ "ptr", wrapper inst; "ptr", envOf inst ]
                | _ ->
                    let v = ex a
                    owned <- (v, a.t) :: owned
                    match p with
                    | Stdlib.SVar _ -> [ "ptr", spill v a.t ]
                    | Stdlib.SBool -> [ "i64", instr "zext i1 %s to i64" v ]
                    | _ -> [ llty a.t, v ]) sg.parms (List.skip nd args)
            |> List.concat
        let r =
            match sg.ret with
            | Stdlib.SVar _ ->
                let out = outCell e.t
                rt "void" sg.csym (descs @ cargs @ [ "ptr", out ]) |> ignore
                loadFrom out e.t
            | Stdlib.SBool -> instr "icmp ne i64 %s, 0" (rt "i64" sg.csym (descs @ cargs))
            | _ -> rt (llty e.t) sg.csym (descs @ cargs)
        for (v, t) in owned do release v t
        r

    /// `void w.<inst>(ptr env, ptr x, ptr out)`: a C-callable entry for a block instance; env holds its capture
    /// pointers, x points at the (borrowed) argument's storage, the owned result is stored to out.
    and wrapper (inst: Inst) : string =
        if wrapped.Add inst.name then
            let saved = c
            c <- { body = StringBuilder(); allocas = StringBuilder(); locals = Dictionary(); caps = Set.empty; tmp = 0 }
            let _, pt = inst.parms.Head
            let caps = inst.caps |> List.mapi (fun i _ -> instr "load ptr, ptr %s" (instr "getelementptr ptr, ptr %%env, i64 %d" i))
            let a = loadFrom "%x" pt
            retain a pt // the instance owns its parameter; C passes a borrowed element
            let r = tmp ()
            let ps = (llty pt + " " + a) :: (caps |> List.map (fun p -> "ptr " + p))
            emit (sprintf "%s = call %s @\"%s\"(%s)" r (llty inst.ret) inst.name (String.concat ", " ps))
            let sr = if resolve inst.ret = TBool then instr "zext i1 %s to i8" r else r
            emit (sprintf "store %s %s, ptr %%out" (Repr.storage inst.ret) sr)
            emit "ret void"
            wrappers.Append(sprintf "define void @\"w.%s\"(ptr %%env, ptr %%x, ptr %%out) {\nentry:\n%s%s}\n\n" inst.name (string c.allocas) (string c.body)) |> ignore
            c <- saved
        sprintf "@\"w.%s\"" inst.name

    and envOf (inst: Inst) : string =
        if inst.caps.IsEmpty then "null"
        else
            let env = alloca (sprintf "[%d x ptr]" inst.caps.Length)
            inst.caps |> List.iteri (fun i u -> emit (sprintf "store ptr %s, ptr %s" (ptrOf u) (instr "getelementptr ptr, ptr %s, i64 %d" env i)))
            env

    and stmt (s: S) =
        match s with
        | Line _ -> ()
        | Do e -> release (ex e) e.t
        | Assign(u, e) when isFn prog.varTy.[u] -> ()
        | Assign(u, e) ->
            let v = ex e
            let p = ptrOf u
            let old = if isPtr e.t then instr "load ptr, ptr %s" p else ""
            emit (sprintf "store %s %s, ptr %s" (llty e.t) v p)
            release old e.t
        | SetIdx(x, i, v) ->
            let [ (xv, xo); (iv, io); (vv, vo) ] = exArgs 0 [ x; i; v ]
            // locate the element's storage, then replace it in place: retain the new value before releasing the old
            let p =
                match resolve x.t with
                | TMap(k, _) -> rt "ptr" "ns_map_put" [ "ptr", xv; "ptr", spill iv k ]
                | _ -> rt "ptr" "ns_arr_at" [ "ptr", xv; "i64", iv ]
            let old = if isPtr v.t then instr "load ptr, ptr %s" p else ""
            retain vv v.t
            storeTo vv v.t p
            release old v.t
            drop (xv, xo) x.t
            drop (iv, io) i.t
            drop (vv, vo) v.t
        | While(cond, body) ->
            let lc, lb, lend = label (), label (), label ()
            emit (sprintf "br label %%%s" lc)
            place lc
            emit (sprintf "br i1 %s, label %%%s, label %%%s" (ex cond) lb lend)
            place lb
            stmts body
            emit (sprintf "br label %%%s" lc)
            place lend

    and stmts ss = List.iter stmt ss

    let newCtx caps = { body = StringBuilder(); allocas = StringBuilder(); locals = Dictionary(); caps = caps; tmp = 0 }
    let out = StringBuilder()
    let fns = StringBuilder()

    for inst in prog.insts do
        c <- newCtx (set inst.caps)
        for (u, t) in inst.parms do emit (sprintf "store %s %%\"p.%s\", ptr %s" (llty t) u (ptrOf u))
        stmts inst.body
        let r = ex inst.result
        for KeyValue(u, p) in List.ofSeq c.locals do
            if isPtr prog.varTy.[u] then release (instr "load ptr, ptr %s" p) prog.varTy.[u]
        emit (sprintf "ret %s %s" (llty inst.ret) r)
        let ps = (inst.parms |> List.map (fun (u, t) -> sprintf "%s %%\"p.%s\"" (llty t) u)) @ (inst.caps |> List.map (fun u -> sprintf "ptr %%\"c.%s\"" u))
        fns.Append(sprintf "define %s @\"%s\"(%s) {\nentry:\n%s%s}\n\n" (llty inst.ret) inst.name (String.concat ", " ps) (string c.allocas) (string c.body)) |> ignore

    c <- newCtx Set.empty
    inMain <- true
    stmts prog.main
    for KeyValue(u, p) in List.ofSeq c.locals do // main's former globals: release like any function's locals
        if isPtr prog.varTy.[u] then release (instr "load ptr, ptr %s" p) prog.varTy.[u]
    emit "ret i32 0"
    fns.Append(sprintf "define i32 @main() {\nentry:\n%s%s}\n" (string c.allocas) (string c.body)) |> ignore

    for (g, t) in prog.globals do
        if not (isFn t) then out.Append(sprintf "@\"g.%s\" = internal global %s %s\n" g (llty t) (if llty t = "ptr" then "null" else "zeroinitializer")) |> ignore
    for KeyValue(s, n) in strings do
        let bytes = Encoding.UTF8.GetBytes s
        let esc = bytes |> Array.map (fun b -> if b >= 32uy && b < 127uy && b <> 34uy && b <> 92uy then string (char b) else sprintf "\\%02X" b) |> String.concat ""
        out.Append(sprintf "%s = private unnamed_addr constant { i64, ptr, [%d x i8] } { i64 -1, ptr null, [%d x i8] c\"%s\\00\" }\n" n (bytes.Length + 1) (bytes.Length + 1) esc) |> ignore
    for KeyValue(_, decl) in decls do out.Append(decl).Append('\n') |> ignore
    out.Append('\n').Append(fns).Append(wrappers).ToString()
