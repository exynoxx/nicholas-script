/// Stage 5: typed core -> textual LLVM IR. Values are i64/double/i1 or ptr; collection elements are 64-bit slots.
module Codegen

open System
open System.Text
open System.Collections.Generic
open Core
open Typer

let rec desc t =
    match resolve t with
    | TFloat -> "F"
    | TStr -> "S"
    | TBool -> "B"
    | TArr e -> "A" + desc e
    | TMap(k, v) -> "M" + desc k + desc v
    | TSet e -> "H" + desc e
    | TQ e -> "Q" + desc e
    | TPQ e -> "P" + desc e
    | _ -> "I"

let llty t =
    match resolve t with
    | TInt | TFn _ | THole _ | TUnk -> "i64"
    | TFloat -> "double"
    | TBool -> "i1"
    | _ -> "ptr"

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
        if globals.Contains u then sprintf "@\"g.%s\"" u
        elif c.caps.Contains u then sprintf "%%\"c.%s\"" u
        else
            match c.locals.TryGetValue u with
            | true, p -> p
            | _ ->
                let ty = llty prog.varTy.[u]
                let p = alloca ty
                if ty = "ptr" then c.allocas.Append(sprintf "  store ptr null, ptr %s\n" p) |> ignore
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
    let toSlot (v: string) t =
        match llty t with
        | "i64" -> v
        | "double" -> instr "bitcast double %s to i64" v
        | "i1" -> instr "zext i1 %s to i64" v
        | _ -> instr "ptrtoint ptr %s to i64" v
    let fromSlot (v: string) t =
        match llty t with
        | "i64" -> v
        | "double" -> instr "bitcast i64 %s to double" v
        | "i1" -> instr "trunc i64 %s to i1" v
        | _ -> instr "inttoptr i64 %s to ptr" v
    let d t = "ptr", strConst (desc t)
    // Reference counting: every expression yields an owned (+1) value; runtime args are borrowed.
    let isPtr t = llty t = "ptr"
    let release (v: string) t = if isPtr t then rt "void" "ns_release" [ "ptr", v ] |> ignore
    let retain (v: string) t = if isPtr t then rt "void" "ns_retain" [ "ptr", v ] |> ignore

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
                rt "void" "ns_arr_push" [ "ptr", a; "i64", toSlot v x.t ] |> ignore
                release v x.t
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
        | Call(f, args) -> call e f args
        | Index(x, i) ->
            let [ (xv, xo); (iv, io) ] = exArgs 0 [ x; i ]
            let r =
                match resolve x.t with
                | TStr -> rt "ptr" "ns_str_at" [ "ptr", xv; "i64", iv ]
                | TMap(k, _) -> fromSlot (rt "i64" "ns_map_get" [ "ptr", xv; "i64", toSlot iv k ]) e.t
                | _ -> fromSlot (rt "i64" "ns_arr_get" [ "ptr", xv; "i64", iv ]) e.t
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
        | s, TStr -> rt "ptr" "ns_tostr" [ d s; "i64", toSlot v s ]
        | s, TBool -> instr "icmp ne i64 %s, 0" (rt "i64" "ns_len" [ d s; "i64", toSlot v s ])
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
            let r = rt "i64" "ns_eq" [ d t; "i64", toSlot av t; "i64", toSlot bv t ]
            instr "icmp %s i64 %s, 0" (if op = "=" then "ne" else "eq") r
        | t, _ when cmpOp.ContainsKey op ->
            let r = rt "i64" "ns_cmp" [ d t; "i64", toSlot av t; "i64", toSlot bv t ]
            let (i, _, _) = cmpOp.[op]
            instr "icmp %s i64 %s, 0" i r
        | TInt, ("/" | "%" | "^^") ->
            rt "i64" (match op with "/" -> "ns_idiv" | "%" -> "ns_imod" | _ -> "ns_ipow") [ "i64", av; "i64", bv ]
        | TInt, _ ->
            let ins = match op with "+" -> "add" | "-" -> "sub" | "*" -> "mul" | "&" -> "and" | "^" -> "xor" | "<<" -> "shl" | ">>" -> "ashr" | _ -> "or"
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
        | "mapof", [ { k = Arr ks }; { k = Arr vs } ] ->
            let m = rt "ptr" "ns_map_new" [ d e.t ]
            for (k, v) in List.zip ks vs do
                let kv, vv = ex k, ex v
                rt "void" "ns_map_set" [ "ptr", m; "i64", toSlot kv k.t; "i64", toSlot vv v.t ] |> ignore
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
        let slot i = "i64", toSlot (arg i) (ty i)
        let elemDesc t = match resolve t with TArr el -> d el | _ -> d TInt
        match f, args with
        | "print", [ x ] -> (rt "void" "ns_print" [ d x.t; slot 0 ] |> ignore; arg 0)
        | "read", [] -> rt "ptr" "ns_read" []
        | "lines", [] -> rt "ptr" "ns_lines" []
        | "neg", [ x ] -> if resolve x.t = TFloat then instr "fneg double %s" (arg 0) else instr "sub i64 0, %s" (arg 0)
        | "not", _ -> instr "xor i1 %s, true" (arg 0)
        | "len", [ x ] -> rt "i64" "ns_len" [ d x.t; slot 0 ]
        | "has", [ x; _ ] -> instr "icmp ne i64 %s, 0" (rt "i64" "ns_has" [ d x.t; slot 0; slot 1 ])
        | "range", _ -> rt "ptr" "ns_range" [ "i64", arg 0; "i64", arg 1 ]
        | "slice", [ x; _; _ ] ->
            rt "ptr" (if resolve x.t = TStr then "ns_slice_s" else "ns_arr_slice") [ "ptr", arg 0; "i64", arg 1; "i64", arg 2 ]
        | ("set" | "Q" | "PQ"), [ xs ] ->
            let el = match resolve e.t with TSet el | TQ el | TPQ el -> el | _ -> TInt
            let ctor = match f with "set" -> "ns_set_new" | "Q" -> "ns_q_new" | _ -> "ns_pq_new"
            let coll = rt "ptr" ctor [ d e.t ]
            rt "void" "ns_add_all" [ d e.t; "ptr", coll; "ptr", arg 0 ] |> ignore
            coll
        | "pop", [ q ] -> fromSlot (rt "i64" "ns_pop" [ d q.t; "ptr", arg 0 ]) e.t
        | "peek", [ q ] -> fromSlot (rt "i64" "ns_peek" [ d q.t; "ptr", arg 0 ]) e.t
        | ("keys" | "arr"), [ x ] -> rt "ptr" "ns_toarr" [ d x.t; slot 0 ]
        | "vals", _ -> rt "ptr" "ns_map_vals" [ "ptr", arg 0 ]
        | "chars", _ -> rt "ptr" "ns_chars" [ "ptr", arg 0 ]
        | "sort", [ a ] -> rt "ptr" "ns_sort" [ elemDesc a.t; "ptr", arg 0 ]
        | "rev", _ -> rt "ptr" "ns_rev" [ "ptr", arg 0 ]
        | "panic", _ -> (rt "void" "ns_panic" [ "ptr", arg 0 ] |> ignore; "0")
        | "split", _ -> rt "ptr" "ns_split" [ "ptr", arg 0; "ptr", arg 1 ]
        | "join", [ a; _ ] -> rt "ptr" "ns_join" [ elemDesc a.t; "ptr", arg 0; "ptr", arg 1 ]
        | "ord", _ -> rt "i64" "ns_ord" [ "ptr", arg 0 ]
        | "chr", _ -> rt "ptr" "ns_chr" [ "i64", arg 0 ]
        | "copy", _ -> rt "ptr" "ns_arr_copy" [ "ptr", arg 0 ]
        | "push", _ -> (rt "void" "ns_arr_push" [ "ptr", arg 0; slot 1 ] |> ignore; arg 0)
        | "append", _ ->
            let cp = rt "ptr" "ns_arr_copy" [ "ptr", arg 0 ]
            rt "void" "ns_arr_push" [ "ptr", cp; slot 1 ] |> ignore
            cp
        | "concat", _ -> rt "ptr" "ns_arr_concat" [ "ptr", arg 0; "ptr", arg 1 ]
        | "add", [ coll; _ ] -> (rt "void" "ns_add" [ d coll.t; "ptr", arg 0; slot 1 ] |> ignore; arg 0)
        | "del", [ coll; _ ] -> (rt "void" "ns_del" [ d coll.t; "ptr", arg 0; slot 1 ] |> ignore; arg 0)
        | "merge", _ -> (rt "void" "ns_map_merge" [ "ptr", arg 0; "ptr", arg 1 ] |> ignore; arg 0)
        | name, _ when name.StartsWith "@" ->
            let inst = instByName.[name.Substring 1]
            let ps = List.zip vs (inst.parms |> List.map snd) |> List.map (fun (v, t) -> llty t + " " + v)
            let cs = inst.caps |> List.map (fun u -> "ptr " + ptrOf u)
            let r = tmp ()
            emit (sprintf "%s = call %s @\"%s\"(%s)" r (llty inst.ret) inst.name (ps @ cs |> String.concat ", "))
            r
        | _ -> failwithf "codegen: unknown call %s/%d" f args.Length

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
            match resolve x.t with
            | TMap(k, _) -> rt "void" "ns_map_set" [ "ptr", xv; "i64", toSlot iv k; "i64", toSlot vv v.t ] |> ignore
            | _ -> rt "void" "ns_arr_set" [ "ptr", xv; "i64", iv; "i64", toSlot vv v.t ] |> ignore
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
    stmts prog.main
    emit "ret i32 0"
    fns.Append(sprintf "define i32 @main() {\nentry:\n%s%s}\n" (string c.allocas) (string c.body)) |> ignore

    for (g, t) in prog.globals do
        if not (isFn t) then out.Append(sprintf "@\"g.%s\" = internal global %s %s\n" g (llty t) (if llty t = "ptr" then "null" else "zeroinitializer")) |> ignore
    for KeyValue(s, n) in strings do
        let bytes = Encoding.UTF8.GetBytes s
        let esc = bytes |> Array.map (fun b -> if b >= 32uy && b < 127uy && b <> 34uy && b <> 92uy then string (char b) else sprintf "\\%02X" b) |> String.concat ""
        out.Append(sprintf "%s = private unnamed_addr constant { i64, ptr, [%d x i8] } { i64 -1, ptr null, [%d x i8] c\"%s\\00\" }\n" n (bytes.Length + 1) (bytes.Length + 1) esc) |> ignore
    for KeyValue(_, decl) in decls do out.Append(decl).Append('\n') |> ignore
    out.Append('\n').Append(fns).ToString()
