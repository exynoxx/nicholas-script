/// Alternative stage 6: typed core -> C source, a line-for-line twin of Codegen (same refcounting, same layouts
/// from Repr, same runtime calls). Control flow is goto-based like the LLVM version; every temporary is a C local.
/// The output is prefixed with runtime.c by Program, so any C compiler can build it.
module CGen

open System
open System.Text
open System.Collections.Generic
open Core
open Typer

let private desc = Repr.desc
let private isFn t = match resolve t with TFn _ -> true | _ -> false

/// NS names contain `.` `%` `@` ...: keep [A-Za-z0-9], hex-escape everything else (injective).
let private mangle (s: string) =
    s |> String.collect (fun ch -> if Char.IsAsciiLetterOrDigit ch then string ch else $"_{int ch:X2}")

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
      locals: StringBuilder // declarations hoisted to the top of the function (LLVM's allocas)
      vars: HashSet<string>
      caps: Set<string>
      mutable tmp: int }

let generate (prog: Program) : string =
    let strings = Dictionary<string, string>()
    let tupTypes = Dictionary<string, string>() // typedef name -> definition
    let globals = prog.globals |> List.map fst |> set
    let instByName = prog.insts |> List.map (fun i -> i.name, i) |> dict
    let mutable c = Unchecked.defaultof<Ctx>
    let fnGlobals = HashSet<string>()
    let mutable inMain = false

    /// C type in registers (Repr.llty's twin); value tuples get a typedef named after their descriptor.
    let cty t =
        match resolve t with
        | TInt | TFn _ | THole _ | TUnk -> "int64_t"
        | TFloat -> "double"
        | TBool -> "bool"
        | TTup ts when Repr.isValueTuple t ->
            let n = "ns_" + desc t
            if not (tupTypes.ContainsKey n) then
                let ms = ts |> List.mapi (fun i m -> (if resolve m = TFloat then "double" else "int64_t") + $" m{i};") |> String.concat " "
                tupTypes.[n] <- $"typedef struct {{ {ms} }} {n};"
            n
        | _ -> "void *"
    let storage t = match resolve t with TBool -> "uint8_t" | _ -> cty t
    let isPtr = Repr.isPtr

    let strConst (s: string) =
        let n =
            match strings.TryGetValue s with
            | true, n -> n
            | _ ->
                let n = $"ns_s{strings.Count}"
                strings.[s] <- n
                n
        $"{n}.s"
    let tmp () =
        c.tmp <- c.tmp + 1
        $"t{c.tmp}"
    let label () =
        c.tmp <- c.tmp + 1
        $"L{c.tmp}"
    let emit (s: string) = c.body.Append("  ").Append(s).Append('\n') |> ignore
    let place (l: string) = c.body.Append(l).Append(":;\n") |> ignore
    let local ty =
        let r = tmp ()
        c.locals.Append($"  {ty} {r} = {{0}};\n") |> ignore
        r
    /// The variable as a C lvalue.
    let lv (u: string) =
        if globals.Contains u && not (inMain && not (fnGlobals.Contains u)) then
            fnGlobals.Add u |> ignore
            "g_" + mangle u
        elif c.caps.Contains u then $"(*({cty prog.varTy.[u]} *)c_{mangle u})"
        else
            let n = "v_" + mangle u
            if c.vars.Add u then c.locals.Append($"  {cty prog.varTy.[u]} {n} = {{0}};\n") |> ignore
            n
    let instr (ty: string) (s: string) = let r = tmp () in emit $"{ty} {r} = {s};"; r
    let rt (ret: string) (name: string) (args: string list) =
        let call = name + "(" + String.concat ", " args + ")"
        if ret = "void" then (emit (call + ";"); "") else instr ret call
    let storeTo (v: string) t (p: string) =
        let s = instr (storage t) v
        emit $"memcpy({p}, &{s}, sizeof {s});"
    let spill (v: string) t =
        let p = local (storage t)
        emit $"{p} = {v};"
        "&" + p
    let loadFrom (p: string) t =
        let r = tmp ()
        emit $"{storage t} {r}; memcpy(&{r}, {p}, sizeof {r});"
        if resolve t = TBool then instr "bool" r else r
    let outCell t = "&" + local (storage t)
    let d t = strConst (desc t)
    let release (v: string) t = if isPtr t then rt "void" "ns_release" [ v ] |> ignore
    let retain (v: string) t = if isPtr t then rt "void" "ns_retain" [ v ] |> ignore

    // a variable no callee can reassign: not captured by any instance, not a global a function touches
    let allCaps = prog.insts |> Seq.collect (fun i -> i.caps) |> HashSet
    let lendable (u: string) =
        not (allCaps.Contains u) && (not (globals.Contains u) || (inMain && not (fnGlobals.Contains u)))

    let wrappers = StringBuilder()
    let protos = StringBuilder()
    let wrapped = HashSet<string>()

    let rec ex (e: E) : string =
        match e.k with
        | Int v -> if v = Int64.MinValue then "INT64_MIN" else $"INT64_C({v})"
        | Float f -> $"ns_f64(UINT64_C(0x{BitConverter.DoubleToInt64Bits f:X16}))"
        | Bool b -> if b then "1" else "0"
        | Str s -> strConst s
        | Var _ when isFn e.t -> "0"
        | Var u ->
            let v = instr (cty e.t) (lv u)
            retain v e.t
            v
        | Fn _ -> "0"
        | Arr xs ->
            let a = rt "void *" "ns_arr_new" [ d e.t; string xs.Length ]
            for x in xs do
                let v = ex x
                storeTo v x.t (rt "void *" "ns_arr_append" [ a ])
            a
        | Bin(op, a, b) -> binop op a b
        | If(cond, a, b) ->
            let res = if isFn e.t then "" else local (cty e.t)
            let lt, le, lend = label (), label (), label ()
            emit $"if ({ex cond}) goto {lt}; else goto {le};"
            for (l, x) in [ lt, a; le, b ] do
                place l
                let v = ex x
                if res <> "" then emit $"{res} = {v};"
                emit $"goto {lend};"
            place lend
            if res = "" then "0" else instr (cty e.t) res
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
            let cs = inst.caps |> List.map (fun u -> "&" + lv u)
            let r = rt (cty inst.ret) ("f_" + mangle inst.name) ((evs |> List.map fst) @ cs)
            List.zip evs args |> List.iter (fun (v, a) -> drop v a.t)
            r
        | Index(x, i) ->
            let [ (xv, xo); (iv, io) ] = exArgs 0 [ x; i ]
            let r =
                match resolve x.t with
                | TStr -> rt "void *" "ns_str_at" [ xv; iv ]
                | TMap(k, _) ->
                    let out = outCell e.t
                    let key = spill iv k
                    let p = rt "void *" "ns_map_ref" [ xv; key ]
                    let lf, ls, lend = label (), label (), label ()
                    emit $"if ({p}) goto {lf}; else goto {ls};"
                    place lf
                    let v = loadFrom p e.t
                    retain v e.t
                    storeTo v e.t out
                    emit $"goto {lend};"
                    place ls
                    rt "void" "ns_map_get" [ xv; key; out ] |> ignore
                    emit $"goto {lend};"
                    place lend
                    loadFrom out e.t
                | _ ->
                    let v = loadFrom (rt "void *" "ns_arr_at" [ xv; iv ]) e.t
                    retain v e.t
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

    and exB (e: E) : string * bool =
        match e.k with
        | Var u when isPtr e.t && not (isFn e.t) -> instr "void *" (lv u), false
        | _ -> ex e, true

    and exArgs (owned: int) (es: E list) =
        es |> List.mapi (fun i e ->
            if i >= owned && List.forall noAssign (List.skip (i + 1) es) then exB e else ex e, true)

    and drop (v: string, owned) t = if owned then release v t

    and convert1 (v: string) (src: Ty) (dst: Ty) =
        match resolve src, resolve dst with
        | TInt, TFloat -> instr "double" $"(double){v}"
        | TFloat, TInt -> instr "int64_t" $"(int64_t){v}"
        | TInt, TBool -> instr "bool" $"{v} != 0"
        | TFloat, TBool -> instr "bool" $"{v} != 0.0"
        | TBool, TInt -> instr "int64_t" v
        | TBool, TFloat -> instr "double" v
        | TStr, TInt -> rt "int64_t" "ns_s2i" [ v ]
        | TStr, TFloat -> rt "double" "ns_s2f" [ v ]
        | s, TStr -> rt "void *" "ns_tostr" [ d s; spill v s ]
        | s, TBool -> instr "bool" (rt "int64_t" "ns_len" [ d s; spill v s ] + " != 0")
        | s, t -> failwith $"cgen: no conversion {showTy s} -> {showTy t}"

    and binop op (a: E) (b: E) =
        let [ (av, ao); (bv, bo) ] = exArgs 0 [ a; b ]
        let r = binop1 op a b av bv
        drop (av, ao) a.t
        drop (bv, bo) b.t
        r

    and binop1 op (a: E) (b: E) av bv =
        let cmpOp = dict [ "=", "=="; "!=", "!="; "<", "<"; ">", ">"; "<=", "<="; ">=", ">=" ]
        // signed overflow and shifts are defined in LLVM (wrapping); do them unsigned in C
        let wrap o = instr "int64_t" $"(int64_t)((uint64_t){av} {o} (uint64_t){bv})"
        match resolve a.t, op with
        | (TInt | TFloat | TBool), _ when cmpOp.ContainsKey op -> instr "bool" $"{av} {cmpOp.[op]} {bv}"
        | TStr, ("=" | "!=") ->
            let r = rt "int64_t" "ns_str_eq" [ av; bv ]
            instr "bool" (if op = "=" then $"{r} != 0" else $"{r} == 0")
        | TTup ts, ("=" | "!=") when Repr.isValueTuple a.t ->
            // value tuple: compare members directly
            let all = ts |> List.mapi (fun i _ -> $"{av}.m{i} == {bv}.m{i}") |> String.concat " && "
            instr "bool" (if op = "=" then all else $"!({all})")
        | t, ("=" | "!=") when t <> TStr ->
            let r = rt "int64_t" "ns_eq" [ d t; spill av t; spill bv t ]
            instr "bool" (if op = "=" then $"{r} != 0" else $"{r} == 0")
        | t, _ when cmpOp.ContainsKey op ->
            let r = rt "int64_t" "ns_cmp" [ d t; spill av t; spill bv t ]
            instr "bool" $"{r} {cmpOp.[op]} 0"
        | TInt, ("/" | "%" | "^^") ->
            rt "int64_t" (match op with "/" -> "ns_idiv" | "%" -> "ns_imod" | _ -> "ns_ipow") [ av; bv ]
        | TInt, ("+" | "-" | "*" | "<<") -> wrap op
        | TInt, ">>>" -> wrap ">>"
        | TInt, _ -> instr "int64_t" $"{av} {op} {bv}" // & ^ | >>
        | TFloat, "^^" -> instr "double" $"pow({av}, {bv})"
        | TFloat, "%" -> instr "double" $"fmod({av}, {bv})"
        | TFloat, _ -> instr "double" $"{av} {op} {bv}"
        | TBool, _ ->
            let o = match op with "+" -> "|" | "*" -> "&" | _ -> "^"
            instr "bool" $"{av} {o} {bv}"
        | TStr, "+" -> rt "void *" "ns_concat" [ av; bv ]
        | TStr, "*" -> rt "void *" "ns_repeat" [ av; bv ]
        | t, _ -> failwith $"cgen: operator {op} on {showTy t}"

    and call (e: E) (f: string) (args: E list) =
        match f, args with
        | "mapof", [ { k = Arr ks }; { k = Arr vs } ] ->
            let m = rt "void *" "ns_map_new" [ d e.t ]
            for (k, v) in List.zip ks vs do
                let kv, vv = ex k, ex v
                rt "void" "ns_map_set" [ m; spill kv k.t; spill vv v.t ] |> ignore
                release kv k.t
                release vv v.t
            m
        | _ ->
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
        let cell i = spill (arg i) (ty i)
        match f, args with
        | ("print" | "println"), [ x ] -> (rt "void" ("ns_" + f) [ d x.t; cell 0 ] |> ignore; arg 0)
        | "neg", [ x ] ->
            if resolve x.t = TFloat then instr "double" $"-{arg 0}" else instr "int64_t" $"(int64_t)(0 - (uint64_t){arg 0})"
        | "not", _ -> instr "bool" $"!{arg 0}"
        | "len", [ x ] -> rt "int64_t" "ns_len" [ d x.t; cell 0 ]
        | "has", [ x; _ ] -> instr "bool" (rt "int64_t" "ns_has" [ d x.t; arg 0; cell 1 ] + " != 0")
        | "range", _ -> rt "void *" "ns_range" [ arg 0; arg 1 ]
        | "slice", [ x; _; _ ] ->
            rt "void *" (if resolve x.t = TStr then "ns_slice_s" else "ns_arr_slice") [ arg 0; arg 1; arg 2 ]
        | "BT", [] -> rt "void *" "ns_bt_new" [ d e.t ] // empty; the typer adds the elements
        | ("set" | "Q" | "PQ"), [ _ ] ->
            let ctor = match f with "set" -> "ns_set_new" | "Q" -> "ns_q_new" | _ -> "ns_pq_new"
            let coll = rt "void *" ctor [ d e.t ]
            rt "void" "ns_add_all" [ d e.t; coll; arg 0 ] |> ignore
            coll
        | "peek", [ q ] ->
            let out = outCell e.t
            rt "void" "ns_peek" [ d q.t; arg 0; out ] |> ignore
            loadFrom out e.t
        | "tuple", _ when Repr.isValueTuple e.t ->
            let ms = args |> List.mapi (fun i a -> (if resolve a.t = TFloat then "" else "(int64_t)") + arg i)
            instr (cty e.t) ("{ " + String.concat ", " ms + " }")
        | "tuple", _ ->
            let t = rt "void *" "ns_tup_new" [ d e.t ]
            args |> List.iteri (fun i _ -> rt "void" "ns_tup_set" [ t; string i; cell i ] |> ignore)
            t
        | "tget", [ x; { k = Int k } ] when Repr.isValueTuple x.t -> instr (cty e.t) $"{arg 0}.m{k}"
        | "tget", [ x; { k = Int k } ] ->
            // boxed tuple: the member sits at a fixed offset in the object; borrowed from it, so retain
            let v = loadFrom $"((char *){arg 0} + {Repr.tupOffset x.t (int k)})" e.t
            retain v e.t
            v
        | "repeat", [ _; _ ] -> rt "void *" "ns_arr_repeat" [ d e.t; cell 0; arg 1 ]
        | "panic", _ -> (rt "void" "ns_panic" [ arg 0 ] |> ignore; "0")
        | "try", _ ->
            // `?? stmt`: a handler frame in this function; _setjmp returns again (nonzero) when the statement panics
            let buf = tmp ()
            c.locals.Append($"  _Alignas(16) char {buf}[512];\n") |> ignore
            rt "void" "ns_try" [ buf ] |> ignore
            instr "bool" $"_setjmp((void *){buf}) == 0"
        | "untry", _ -> (rt "void" "ns_untry" [] |> ignore; "0")
        | "copy", _ -> rt "void *" "ns_arr_copy" [ arg 0 ]
        | "push", _ ->
            storeTo (arg 1) (ty 1) (rt "void *" "ns_arr_append" [ arg 0 ])
            retain (arg 1) (ty 1)
            arg 0
        | "append", _ ->
            let cp = rt "void *" "ns_arr_copy" [ arg 0 ]
            storeTo (arg 1) (ty 1) (rt "void *" "ns_arr_append" [ cp ])
            retain (arg 1) (ty 1)
            cp
        | "concat", _ -> rt "void *" "ns_arr_concat" [ arg 0; arg 1 ]
        | "add", [ coll; _ ] -> (rt "void" "ns_add" [ d coll.t; arg 0; cell 1 ] |> ignore; arg 0)
        | "del", [ coll; _ ] -> (rt "void" "ns_del" [ d coll.t; arg 0; cell 1 ] |> ignore; arg 0)
        | "merge", _ -> (rt "void" "ns_map_merge" [ arg 0; arg 1 ] |> ignore; arg 0)
        | _ -> failwith $"cgen: unknown call {f}/{args.Length}"

    and stdCall (e: E) (sg: Stdlib.Sig) (args: E list) =
        let nd = args.Length - sg.parms.Length // descriptors
        let descs = args |> List.take nd |> List.map ex
        let mutable owned = []
        let cargs =
            List.map2 (fun (p: Stdlib.STy) (a: E) ->
                match p, a.k with
                | Stdlib.SFn _, BlockRef b ->
                    let inst = instByName.[b]
                    [ wrapper inst; envOf inst ]
                | _ ->
                    let v = ex a
                    owned <- (v, a.t) :: owned
                    match p with
                    | Stdlib.SVar _ | Stdlib.STup _ -> [ spill v a.t ]
                    | Stdlib.SBool -> [ instr "int64_t" v ]
                    | _ -> [ v ]) sg.parms (List.skip nd args)
            |> List.concat
        let r =
            match sg.ret with
            | Stdlib.SVar _ | Stdlib.STup _ ->
                let out = outCell e.t
                rt "void" sg.csym (descs @ cargs @ [ out ]) |> ignore
                loadFrom out e.t
            | Stdlib.SBool -> instr "bool" (rt "int64_t" sg.csym (descs @ cargs) + " != 0")
            | _ -> rt (cty e.t) sg.csym (descs @ cargs)
        for (v, t) in owned do release v t
        r

    /// `void w_<inst>(void *env, const void *x, void *out)`: the runtime's ns_fn entry for a block instance.
    and wrapper (inst: Inst) : string =
        let w = "w_" + mangle inst.name
        if wrapped.Add inst.name then
            let saved = c
            c <- { body = StringBuilder(); locals = StringBuilder(); vars = HashSet(); caps = Set.empty; tmp = 0 }
            let caps = inst.caps |> List.mapi (fun i _ -> instr "void *" $"((void **)env)[{i}]")
            // one parameter: x points at it; several: x is an array of pointers to them
            let args =
                inst.parms |> List.mapi (fun i (u, pt) ->
                    let a = loadFrom (if inst.parms.Length = 1 then "x" else $"((void *const *)x)[{i}]") pt
                    if not (inst.borrowed.Contains u) then retain a pt
                    a)
            let r = rt (cty inst.ret) ("f_" + mangle inst.name) (args @ caps)
            storeTo r inst.ret "out"
            let sg = $"static void {w}(void *env, const void *x, void *out)"
            protos.Append(sg + ";\n") |> ignore
            wrappers.Append($"{sg} {{\n{c.locals}{c.body}}}\n\n") |> ignore
            c <- saved
        w

    and envOf (inst: Inst) : string =
        if inst.caps.IsEmpty then "NULL"
        else
            let env = tmp ()
            c.locals.Append($"  void *{env}[{inst.caps.Length}];\n") |> ignore
            inst.caps |> List.iteri (fun i u -> emit $"{env}[{i}] = &{lv u};")
            env

    and stmt (s: S) =
        match s with
        | Line _ -> ()
        | Do e -> release (ex e) e.t
        | Assign(u, _) when isFn prog.varTy.[u] -> ()
        | Assign(u, e) ->
            let v = ex e
            let p = lv u
            let old = if isPtr e.t then instr "void *" p else ""
            emit $"{p} = {v};"
            release old e.t
        | SetIdx(x, i, v) ->
            let [ (xv, xo); (iv, io); (vv, vo) ] = exArgs 0 [ x; i; v ]
            let p =
                match resolve x.t with
                | TMap(k, _) -> rt "void *" "ns_map_put" [ xv; spill iv k ]
                | _ -> rt "void *" "ns_arr_at" [ xv; iv ]
            let old = if isPtr v.t then instr "void *" $"*(void **){p}" else ""
            retain vv v.t
            storeTo vv v.t p
            release old v.t
            drop (xv, xo) x.t
            drop (iv, io) i.t
            drop (vv, vo) v.t
        | While(cond, body) ->
            let lc, lb, lend = label (), label (), label ()
            place lc
            emit $"if ({ex cond}) goto {lb}; else goto {lend};"
            place lb
            stmts body
            emit $"goto {lc};"
            place lend

    and stmts ss = List.iter stmt ss

    let newCtx caps = { body = StringBuilder(); locals = StringBuilder(); vars = HashSet(); caps = caps; tmp = 0 }
    let releaseLocals (borrowed: HashSet<string>) =
        for u in List.ofSeq c.vars do
            if isPtr prog.varTy.[u] && not (borrowed.Contains u) then release (lv u) prog.varTy.[u]
    let fns = StringBuilder()

    for inst in prog.insts do
        c <- newCtx (set inst.caps)
        for (u, _) in inst.parms do emit $"{lv u} = p_{mangle u};"
        stmts inst.body
        let r = ex inst.result
        releaseLocals inst.borrowed
        emit $"return {r};"
        let ps = (inst.parms |> List.map (fun (u, t) -> $"{cty t} p_{mangle u}")) @ (inst.caps |> List.map (fun u -> $"void *c_{mangle u}"))
        let parmList = if ps.IsEmpty then "void" else String.concat ", " ps
        let sg = $"static {cty inst.ret} f_{mangle inst.name}({parmList})"
        protos.Append(sg + ";\n") |> ignore
        fns.Append($"{sg} {{\n{c.locals}{c.body}}}\n\n") |> ignore

    c <- newCtx Set.empty
    inMain <- true
    stmts prog.main
    releaseLocals (HashSet())
    emit "return 0;"
    fns.Append($"int main(void) {{\n{c.locals}{c.body}}}\n") |> ignore

    let out = StringBuilder()
    out.Append("// ---- NS program ----\n#include <stdbool.h>\n")
       .Append("static inline double ns_f64(uint64_t b) { double f; memcpy(&f, &b, 8); return f; }\n") |> ignore
    let globalDecls = StringBuilder()
    for (g, t) in prog.globals do
        if not (isFn t) then globalDecls.Append($"static {cty t} g_{mangle g} = {{0}};\n") |> ignore
    for KeyValue(_, def) in tupTypes do out.Append(def).Append('\n') |> ignore
    for KeyValue(s, n) in strings do
        let bytes = Encoding.UTF8.GetBytes s
        let esc = bytes |> Array.map (fun b -> if b >= 32uy && b < 127uy && b <> 34uy && b <> 92uy && b <> 63uy then string (char b) else "\\" + Convert.ToString(int b, 8).PadLeft(3, '0')) |> String.concat ""
        out.Append($"static struct {{ int64_t rc; const char *d; char s[{bytes.Length + 1}]; }} {n} = {{ -1, NULL, \"{esc}\" }};\n") |> ignore
    out.Append(globalDecls).Append(protos).Append('\n').Append(fns).Append(wrappers).ToString()
