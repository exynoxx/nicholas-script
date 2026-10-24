/// `ns explain`: prints a program as readable, Python-flavoured pseudocode.
/// It reads the desugared core, where maps, filters and folds are still `Iter` nodes, and expands them here into
/// `for` loops and comprehensions (the typed core has them lowered into counter loops, which read worse).
/// The typer's result only supplies types: trailing type comments and the shape of an iteration (range, map, string).
module Explain

open System
open System.Collections.Generic
open Ast
open Core

// ---------- colour ----------

let mutable private color = false
let private paint (code: string) (s: string) = if color && s <> "" then $"\x1b[{code}m{s}\x1b[0m" else s
let private kw = paint "35"
let private num = paint "33"
let private strc = paint "32"
let private com = paint "2;3"
let private fnc = paint "36"
let private defc = paint "1;36"
let private visibleLen (s: string) = Text.RegularExpressions.Regex.Replace(s, "\x1b\\[[0-9;]*m", "").Length

// ---------- state ----------

let private varTy = Dictionary<string, Ty>()           // core name (typer suffix stripped) -> type
let private fnInst = Dictionary<int, Typer.Inst>()     // block id -> its first typed instance
let private names = Dictionary<string, string>()       // core name -> display name
let private reserved = HashSet<string>()               // the program's own names: never given to a fresh variable
let private taken = HashSet<string>()                  // display names allocated to fresh variables in scope
let private typed = HashSet<string>()                  // variables whose type comment was shown
let mutable private out = ResizeArray<string>()
let private pending = Dictionary<string, E>()          // a map stage's value, shown at its single use
let mutable private probing = false                  // rendering only to see whether an expression fits inline
let mutable private inFString = false                // strings inside an f-string's braces use single quotes
let mutable private forceIx = false                  // the next loop header binds the index/key even if unused
let mutable private curInd = 0                         // indent for statements hoisted out of an expression

let private emit ind (s: string) = out.Add(String.replicate ind "    " + s)
let private withComment (s: string) (c: string option) =
    match c with
    | Some c -> s + String(' ', max 2 (28 - visibleLen s)) + com ("# " + c)
    | None -> s

/// Runs `f` into a scratch buffer: its result and the lines it would have emitted.
let private capture (f: unit -> 'a) : 'a * ResizeArray<string> =
    let saved, savedInd = out, curInd
    let savedNames, savedTaken = Dictionary(names), HashSet(taken)
    out <- ResizeArray()
    try
        let r = f ()
        r, out
    finally
        out <- saved
        curInd <- savedInd
        names.Clear()
        for kv in savedNames do names.[kv.Key] <- kv.Value
        taken.Clear()
        taken.UnionWith savedTaken

let private block ind (f: unit -> unit) =
    let n = out.Count
    f ()
    if out.Count = n then emit ind (kw "pass")

// ---------- names ----------

let private pyKeywords =
    set [ "and"; "or"; "not"; "in"; "is"; "if"; "else"; "elif"; "for"; "while"; "def"; "return"; "lambda"; "pass"; "del"
          "class"; "try"; "except"; "None"; "True"; "False"; "import"; "from"; "as"; "with"; "yield"; "global"; "break"
          "continue"; "raise"; "assert"; "finally"; "nonlocal" ]

/// `p'3` (a named parameter of block 3) is shown as `p`.
let private userName (n: string) =
    let b = match n.IndexOf '\'' with -1 -> n | k -> n.Substring(0, k)
    if pyKeywords.Contains b then b + "_" else b

let private isFresh (n: string) = n.Contains '%' || (n.StartsWith "_" && n.Length > 1 && Char.IsDigit n.[1])

let private alloc (cands: string list) =
    let ok c = not (taken.Contains c || reserved.Contains c)
    let d =
        match List.tryFind ok cands with
        | Some c -> c
        | None -> Seq.initInfinite (fun i -> List.head cands + string (i + 2)) |> Seq.find ok
    taken.Add d |> ignore
    d

let private bind (n: string) (cands: string list) =
    let d = alloc cands
    names.[n] <- d
    d

/// Forgets a variable's display name; the name is free again once no other variable (an alias) shows it.
let private release (n: string) =
    match names.TryGetValue n with
    | true, d ->
        names.Remove n |> ignore
        if not (names.ContainsValue d) then taken.Remove d |> ignore
    | _ -> ()

let private disp (n: string) =
    match names.TryGetValue n with
    | true, d -> d
    | _ when isFresh n ->
        let hint =
            match n.Split('%').[0] with
            | "_" -> [ "x"; "y"; "z" ]
            | "@" -> [ "i"; "j" ]
            | "$" -> [ "acc" ]
            | "k" -> [ "key" ]
            | "m" -> [ "m" ]
            | p when p.StartsWith "_" -> [ "x" ]
            | _ -> [ "tmp" ]
        bind n hint
    | _ -> userName n

// ---------- types ----------

let private tyOfVar (n: string) = match varTy.TryGetValue n with | true, t when t <> TUnk -> Some(resolve t) | _ -> None

let rec private ofSTy (t: Stdlib.STy) : Ty option =
    match t with
    | Stdlib.SInt -> Some TInt
    | Stdlib.SFloat -> Some TFloat
    | Stdlib.SStr -> Some TStr
    | Stdlib.SBool -> Some TBool
    | Stdlib.SArr e -> ofSTy e |> Option.map TArr
    | _ -> None

let private stripMark (f: string) = if f.StartsWith callMark || f.StartsWith nameMark || f.StartsWith negMark then f.Substring 1 else f

let private elemTy t =
    match t with
    | Some(TArr e) | Some(TSet e) | Some(TQ e) | Some(TPQ e) | Some(TTree(e, _)) -> Some(resolve e)
    | Some TStr -> Some TStr
    | Some(TMap(_, v)) -> Some(resolve v)
    | Some TInt -> Some TInt
    | _ -> None

/// A best-effort type for an untyped core expression (enough to pick `//`, `and`, `.append`, ...).
let rec private tyOf (e: E) : Ty option =
    match e.k with
    | Int _ -> Some TInt
    | Float _ -> Some TFloat
    | Str _ -> Some TStr
    | Bool _ -> Some TBool
    | Var n -> tyOfVar n
    | Arr(x :: _) -> Some(TArr(defaultArg (tyOf x) TUnk))
    | Arr [] -> Some(TArr TUnk)
    | Bin(("=" | "!=" | "<" | ">" | "<=" | ">="), _, _) -> Some TBool
    | Bin("/", a, _) when tyOf a = Some TStr -> Some(TArr TStr)
    | Bin(("&" | "|" | "^"), a, b) when tyOf b = Some TBool -> tyOf a |> Option.orElse (Some TBool)
    | Bin(_, a, _) -> tyOf a
    | If(_, a, b) -> tyOf a |> Option.orElse (tyOf b)
    | Call(("not" | "notbool" | "has" | "bool"), _) -> Some TBool
    | Call(("len" | "int"), _) -> Some TInt
    | Call("float", _) -> Some TFloat
    | Call("str", _) -> Some TStr
    | Call("range", _) -> Some(TArr TInt)
    | Call(("neg" | "slice" | "repeat" | "destructure"), x :: _) -> (match e.k with Call("repeat", _) -> tyOf x |> Option.map TArr | _ -> tyOf x)
    | Call("tuple", xs) -> Some(TTup(xs |> List.map (tyOf >> Option.defaultValue TUnk)))
    | Call("mapof", _) -> Some(TMap(TUnk, TUnk))
    | Call("set", _) -> Some(TSet TUnk)
    | Call("pop", [ q ]) -> elemTy (tyOf q)
    | Call(f, args) ->
        let f = stripMark f
        match tyOfVar f with
        | Some(TFn id) -> (match fnInst.TryGetValue id with | true, i -> Some(resolve i.ret) | _ -> None)
        | _ ->
            match f, args with
            | ("int" | "len" | "ord" | "find"), _ -> Some TInt
            | ("str" | "chr" | "join" | "read" | "lower" | "upper" | "trim" | "hex" | "base"), _ -> Some TStr
            | ("sort" | "rev" | "take" | "uniq"), x :: _ -> tyOf x
            | ("min" | "max" | "sum" | "prod"), [ x ] -> elemTy (tyOf x)
            | ("min" | "max" | "abs"), x :: _ -> tyOf x
            | _ ->
                Stdlib.overloads f
                |> List.tryFind (fun s -> s.parms.Length = args.Length)
                |> Option.bind (fun s -> ofSTy s.ret)
    | Index(x, i) ->
        match tyOf x, i.k with
        | Some(TTup ts), Int k when int k < ts.Length -> Some(resolve ts.[int k])
        | Some TInt, _ -> None
        | t, _ -> elemTy t
    | Iter it ->
        match it.kind with
        | IFold -> tyOfVar it.acc
        | IFilter -> tyOf it.src
        | IMap -> (match tyOf it.src with Some(TMap _ as t) -> Some t | _ -> Some(TArr TUnk))
        | _ -> Some(TArr TUnk)
    | Seq(_, x) -> tyOf x
    | _ -> None

let rec private pyTy (t: Ty) =
    match resolve t with
    | TInt -> "int"
    | TFloat -> "float"
    | TStr -> "str"
    | TBool -> "bool"
    | TArr e -> $"list[{pyTy e}]"
    | TMap(k, v) -> $"dict[{pyTy k}, {pyTy v}]"
    | TSet e -> $"set[{pyTy e}]"
    | TQ e -> $"Queue[{pyTy e}]"
    | TPQ e -> $"MinHeap[{pyTy e}]"
    | TGraph e -> $"Graph[{pyTy e}]"
    | TTree(e, _) -> $"SortedSet[{pyTy e}]"
    | TTup ts -> "(" + (ts |> List.map pyTy |> String.concat ", ") + ")"
    | TFn _ -> "function"
    | THole _ | TUnk -> "?"

/// The type comment for the first assignment of a variable: containers only (scalars are obvious from the code).
let private typeComment (n: string) =
    if isFresh n || not (typed.Add n) then None
    else
        match tyOfVar n with
        | Some(TArr _ | TMap _ | TSet _ | TQ _ | TPQ _ | TGraph _ | TTree _ | TTup _ as t) ->
            let s = pyTy t
            if s.Contains "?" then None else Some s
        | _ -> None

// ---------- core helpers ----------

let rec private visit (f: E -> unit) (e: E) =
    f e
    let v = visit f
    match e.k with
    | Arr xs | Call(_, xs) -> List.iter v xs
    | Bin(_, a, b) | Index(a, b) -> (v a; v b)
    | If(c, a, b) -> (v c; v a; v b)
    | Iter it -> (v it.src; visitS f it.body)
    | Fn(_, b) -> visitS f b
    | Conv x -> v x
    | Seq(ss, x) -> (visitS f ss; v x)
    | MatchOn(s, _, a, _) -> (v s; v a)
    | _ -> ()

and private visitS (f: E -> unit) (ss: S list) =
    for s in ss do
        match s with
        | Assign(n, e) -> (f { k = Var("=" + n); t = TUnk }; visit f e) // assignment targets show up as Var "=name"
        | SetIdx(a, b, c) -> (visit f a; visit f b; visit f c)
        | While(c, b) -> (visit f c; visitS f b)
        | Do e -> visit f e
        | Line _ -> ()

let private countIn (n: string) (ss: S list) (tail: E option) =
    let mutable c = 0
    let f (e: E) = match e.k with Var m when m = n -> c <- c + 1 | _ -> ()
    visitS f ss
    tail |> Option.iter (visit f)
    c

let private occurs n (e: E) = countIn n [] (Some e) > 0
let private assignedIn (n: string) (ss: S list) = countIn ("=" + n) ss None > 0

let rec private rw (f: E -> E option) (e: E) : E =
    match f e with
    | Some r -> r
    | None ->
        let r = rw f
        let k =
            match e.k with
            | Arr xs -> Arr(List.map r xs)
            | Bin(op, a, b) -> Bin(op, r a, r b)
            | If(c, a, b) -> If(r c, r a, r b)
            | Call(n, xs) -> Call(n, List.map r xs)
            | Index(a, b) -> Index(r a, r b)
            | Iter it -> Iter { it with src = r it.src; body = rwS f it.body }
            | Fn(id, b) -> Fn(id, rwS f b)
            | Conv x -> Conv(r x)
            | Seq(ss, x) -> Seq(rwS f ss, r x)
            | MatchOn(s, t, a, fl) -> MatchOn(r s, t, r a, fl)
            | k -> k
        { e with k = k }

and private rwS f (ss: S list) =
    ss |> List.map (function
        | Assign(n, e) -> Assign(n, rw f e)
        | SetIdx(a, b, c) -> SetIdx(rw f a, rw f b, rw f c)
        | While(c, b) -> While(rw f c, rwS f b)
        | Do e -> Do(rw f e)
        | Line n -> Line n)

let private subst (n: string) (v: E) = rw (fun e -> match e.k with Var m when m = n -> Some v | _ -> None)

let private real (ss: S list) = ss |> List.filter (function Line _ -> false | _ -> true)

let private splitBody (body: S list) =
    match List.rev (real body) with
    | Do e :: rest -> List.rev rest, e
    | _ -> real body, mk (Int 0L)

let rec private isPure (e: E) =
    match e.k with
    | Int _ | Float _ | Str _ | Bool _ | Var _ -> true
    | Arr xs | Call("tuple", xs) -> List.forall isPure xs
    | Seq([], x) -> isPure x
    | _ -> false

let private isZero (e: E) = match e.k with Int 0L -> true | Seq(ss, { k = Int 0L }) -> (real ss).IsEmpty | _ -> false

let private multi (lhs: E list) (values: E list) = Do(mk (Call("%multi", [ mk (Arr lhs); mk (Arr values) ])))

/// Undoes desugaring temps: `a,b = b,a` and `x,y = e` come back as one multiple assignment; a temp used once in the
/// next statement, or bound to a plain variable or literal, is substituted.
let rec private prep (ss: S list) (tail: E option) : S list * E option =
    let isT (n: string) = n.StartsWith "t%"
    let lhsOf (s: S) (value: E -> bool) =
        match s with
        | Assign(lv, v) when value v -> Some(mk (Var lv))
        | SetIdx(x, i, v) when value v -> Some(mk (Index(x, i)))
        | _ -> None
    match ss with
    | Assign(t, { k = Call("destructure", [ e; { k = Int n } ]) }) :: rest when isT t && (real rest).Length >= int n ->
        let parts = List.truncate (int n) rest
        let lhs = parts |> List.mapi (fun i s -> lhsOf s (fun v -> match v.k with Index({ k = Var u }, { k = Int j }) -> u = t && j = int64 i | _ -> false))
        if List.forall Option.isSome lhs && countIn t (List.skip (int n) rest) tail = 0 then
            prep (multi (List.choose id lhs) [ e ] :: List.skip (int n) rest) tail
        else
            let ss', tail' = prep rest tail
            ss.Head :: ss', tail'
    | Assign(t, _) :: _ when isT t ->
        let run = ss |> List.takeWhile (function Assign(u, _) -> isT u | _ -> false)
        let k = run.Length
        let temps = run |> List.map (function Assign(u, v) -> u, v | _ -> failwith "unreachable")
        let after = List.skip k ss
        let sinks = List.truncate k after
        let tempOf (v: E) = match v.k with Var u -> temps |> List.tryFind (fun (t, _) -> t = u) | _ -> None
        let lhs = sinks |> List.map (fun s -> match s with Assign(_, v) | SetIdx(_, _, v) -> tempOf v |> Option.bind (fun tv -> lhsOf s (fun _ -> true) |> Option.map (fun l -> l, snd tv)) | _ -> None)
        if k >= 2 && sinks.Length = k && List.forall Option.isSome lhs && (List.choose id lhs |> List.map snd |> List.distinct |> List.length) = k then
            let pairs = List.choose id lhs
            prep (multi (List.map fst pairs) (List.map snd pairs) :: List.skip k after) tail
        else inlineTemp ss tail
    | Assign(_, { k = Index({ k = Var v }, { k = Int 0L }) }) :: _ when (match tyOfVar v with Some(TTup ts) -> ts.Length > 1 && (real ss).Length >= ts.Length | _ -> false) ->
        // `r = p@0; c = p@1` taking every part of a tuple: `r, c = p`
        let n = match tyOfVar v with Some(TTup ts) -> ts.Length | _ -> 0
        let parts = List.truncate n ss
        let lhs = parts |> List.mapi (fun i s -> match s with Assign(x, { k = Index({ k = Var v' }, { k = Int j }) }) when v' = v && j = int64 i && x <> v -> Some x | _ -> None)
        if List.forall Option.isSome lhs then prep (multi (lhs |> List.choose id |> List.map (Var >> mk)) [ mk (Var v) ] :: List.skip n ss) tail
        else
            let rest', tail' = prep ss.Tail tail
            ss.Head :: rest', tail'
    | Assign(t, _) :: _ when t.Contains '%' && not (t.StartsWith "_%" || t.StartsWith "@%" || t.StartsWith "$%") -> inlineTemp ss tail
    | Assign(f, { k = Fn(_, body) }) :: rest when f.StartsWith "not'" && (real body).Length = 1 ->
        let calls = ref 0
        visitS (fun e -> match e.k with Call(n, _) when stripMark n = f -> calls.Value <- calls.Value + 1 | _ -> ()) rest
        tail |> Option.iter (visit (fun e -> match e.k with Call(n, _) when stripMark n = f -> calls.Value <- calls.Value + 1 | _ -> ()))
        match real body, calls.Value with
        | [ Do value ], 1 when countIn ("=" + f) rest None = 0 ->
            let beta =
                rwS (fun e ->
                    match e.k with
                    | Call(n, args) when stripMark n = f ->
                        Some(args |> List.indexed |> List.fold (fun v (i, a) -> subst $"_{i + 1}" a v) value)
                    | _ -> None)
            let tail' = tail |> Option.map (fun t -> match beta [ Do t ] with [ Do t' ] -> t' | _ -> t)
            prep (beta rest) tail'
        | _ ->
            let rest', tail' = prep rest tail
            ss.Head :: rest', tail'
    | s :: rest ->
        let rest', tail' = prep rest tail
        s :: rest', tail'
    | [] -> [], tail

and private inlineTemp (ss: S list) (tail: E option) =
    match ss with
    | Assign(t, v) :: rest ->
        let uses = countIn t rest tail
        // a variable, literal or `v@i` whose variables stay unchanged
        let rec plain (e: E) =
            match e.k with
            | Int _ | Float _ | Str _ | Bool _ -> true
            | Var u -> not (assignedIn u rest)
            | Index(a, b) -> plain a && plain b
            | Call("tuple", xs) -> List.forall plain xs
            | _ -> false
        let trivial = plain v
        let next = real rest |> List.tryHead
        let usedNext = match next with Some(While _) | None -> false | Some s -> countIn t [ s ] None = uses
        if not (assignedIn t rest) && (trivial || (uses = 1 && usedNext)) then
            prep (rwS (fun e -> match e.k with Var m when m = t -> Some v | _ -> None) rest) (tail |> Option.map (subst t v))
        elif uses = 0 && countIn t [] tail = 0 then prep (Do v :: rest) tail
        else
            let rest', tail' = prep rest tail
            ss.Head :: rest', tail'
    | _ -> prep ss tail

// ---------- expressions ----------

let private fmtFloat (v: float) =
    let s = v.ToString("R", Globalization.CultureInfo.InvariantCulture)
    if s.Contains '.' || s.Contains 'E' || s.Contains 'N' || s.Contains 'I' then s else s + ".0"

let private bitOps = set [ "|"; "&"; "^"; "<<"; ">>"; ">>>" ]
let private cmpOps = set [ "="; "!="; "<"; ">"; "<="; ">=" ]

type private Sink =
    | Return
    | Discard
    | To of string * string option ref * string option // lhs text, type comment (shown once), the variable assigned
    | Multi of string list

/// Python precedence: 1 conditional, 2 or, 3 and, 4 not, 5 compare/in, 6 |, 7 ^, 8 &, 9 shifts, 10 + -, 11 * / %,
/// 12 unary minus, 13 **, 14 atoms, calls and indexing.
let rec private ex (e: E) : string * int =
    match e.k with
    | Int v -> num (string v), (if v < 0L then 12 else 14)
    | Float v -> num (fmtFloat v), (if v < 0.0 then 12 else 14)
    | Str s when inFString -> strc ("'" + (let q = quote s in q.Substring(1, q.Length - 2)).Replace("'", "\\'") + "'"), 14
    | Str s -> strc (quote s), 14
    | Bool b -> num (if b then "True" else "False"), 14
    | Var n when pending.ContainsKey n -> ex pending.[n]
    | Var n -> disp n, 14
    | Arr xs -> "[" + list xs + "]", 14
    | Bin("+", _, _) when isInterp e -> fstring e, 14
    | Bin(op, a, b) -> bin op a b
    | If({ k = Call("try", []) }, _, _) -> fallback e
    | If(c, { k = Bool true }, { k = Bool false }) -> ex c
    | If(c, { k = Bool false }, { k = Bool true }) -> ex (mk (Call("not", [ c ])))
    | If({ k = Bool true }, a, _) -> ex a
    | If({ k = Bool false }, _, b) -> ex b
    | If(c, { k = Bool true }, b) when tyOf b = Some TBool -> ex (mk (Bin("|", c, b)))
    | If(c, a, { k = Bool false }) when tyOf a = Some TBool -> ex (mk (Bin("&", c, a)))
    | If(c, a, b) ->
        // a branch with statements can't be inline: a temporary set by an if/else
        let text, hoisted = capture (fun () -> exP 2 a + " " + kw "if" + " " + exP 2 (cond c) + " " + kw "else" + " " + exP 1 b)
        if hoisted.Count = 0 || probing then (out.AddRange hoisted; text, 1)
        else
            let t = alloc [ "value"; "res" ]
            stmtValue curInd (To(t, ref None, None)) { e with k = If(c, a, b) }
            t, 14
    | Call(f, args) -> call e f args
    | Index(x, i) -> exP 14 x + "[" + str i + "]", 14
    | Seq(ss, x) ->
        let ss, x = prep ss (Some x)
        emitAll curInd ss
        ex x.Value
    | Iter it -> iterExpr it, 14
    | MatchOn(s, t, arms, filter) -> ex (matchAsCore s t arms filter)
    | Fn(id, body) -> lambda id body, 1
    | Conv x -> ex x
    | _ -> fallback e

and private fallback (e: E) = showE e, 14
and private str (e: E) = fst (ex e)

/// A condition: an int or float tested for truth reads as `x != 0`.
and private cond (e: E) =
    match e.k with
    | Call("len", _) -> { e with k = Bin(">", e, mk (Int 0L)) }
    | _ ->
    match tyOf e with
    | Some(TInt | TFloat) -> { e with k = Bin("!=", e, mk (Int 0L)) }
    | _ -> e
and private list (xs: E list) = xs |> List.map str |> String.concat ", "

and private exP (p: int) (e: E) =
    let s, q = ex e
    if q < p then "(" + s + ")" else s

and private isInterp (e: E) =
    let rec leftmost (e: E) = match e.k with Bin("+", a, _) -> leftmost a | _ -> e
    match (leftmost e).k with Str _ -> true | _ -> false

/// `"in: " + x` (from interpolation, or a string on the left converting the rest) as an f-string.
and private fstring (e: E) =
    let rec parts (e: E) = match e.k with Bin("+", a, b) -> parts a @ [ b ] | _ -> [ e ]
    let lit (s: string) = (let q = quote s in q.Substring(1, q.Length - 2)).Replace("{", "{{").Replace("}", "}}")
    let ps = parts e
    if ps |> List.forall (fun p -> match p.k with Str _ -> true | _ -> false) then
        strc (quote (ps |> List.map (fun p -> match p.k with Str s -> s | _ -> "") |> String.concat ""))
    else
        let body =
            ps |> List.map (fun p ->
                match p.k with
                | Str s -> strc (lit s)
                | _ ->
                    let saved = inFString
                    inFString <- true
                    let inner = try str p finally inFString <- saved
                    strc "{" + inner + strc "}")
        strc "f\"" + String.concat "" body + strc "\""

and private bin op a b : string * int =
    let isBool (x: E) = tyOf x = Some TBool
    let infix (sym: string) p =
        let side minP (x: E) =
            let s, q = ex x
            let clarity =
                match x.k with
                | Bin(o, _, _) when (bitOps.Contains op || op = "^^") && o <> op && not (cmpOps.Contains o) -> true
                | Bin(o, _, _) when cmpOps.Contains op && bitOps.Contains o && not (isBool x) -> true
                | _ -> false
            if q < minP || clarity then "(" + s + ")" else s
        let lp, rp = if p = 5 then p + 1, p + 1 elif p = 13 then 14, 12 else p, p + 1
        side lp a + " " + sym + " " + side rp b, p
    match op with
    | ("+" | "-" | "*" | "/" | "%" | "^^") when (match tyOf a with Some(TArr _) -> true | _ -> false) -> elementwise op a b
    | "|" when isBool a || isBool b -> infix (kw "or") 2
    | "&" when isBool a || isBool b -> infix (kw "and") 3
    | "=" -> infix "==" 5
    | "!=" | "<" | ">" | "<=" | ">=" -> infix op 5
    | "|" -> infix "|" 6
    | "^" -> infix "^" 7
    | "&" -> infix "&" 8
    | "<<" | ">>" | ">>>" -> infix op 9
    | "^^" -> infix "**" 13
    | "**" when tyOf a = Some TStr -> infix "*" 11
    | "**" -> fnc "repeat" + "(" + list [ a; b ] + ")", 14
    | "/" when tyOf a = Some TStr -> method a "split" [ b ]
    | "/" when tyOf a = Some TInt -> infix "//" 11
    | "*" | "/" | "%" -> infix op 11
    | "++" -> if appends a b then exP 10 a + " + [" + str b + "]", 10 else infix "+" 10
    | "+" when tyOf a = Some TStr && (match tyOf b with Some TStr | None -> false | _ -> true) ->
        exP 10 a + " + " + fnc "str" + "(" + str b + ")", 10
    | "+" when (match tyOf a with Some(TSet _) -> true | _ -> false) && (match tyOf b with Some(TSet _) -> true | _ -> false) -> infix "|" 6
    | "+" | "-" -> infix op 10
    | "??" ->
        match a.k with
        | Call("mapof", [ { k = Arr [] }; _ ]) -> fnc "defaultdict" + "(" + str b + ")", 14
        | _ -> fnc "with_default" + "(" + list [ a; b ] + ")", 14
    | _ -> infix op 10

/// `a ++ b`: whether b is one element appended to the array a (rather than an array concatenated).
and private appends (a: E) (b: E) =
    match tyOf a, tyOf b with
    | Some(TArr el), Some tb when pyTy tb = pyTy el && not ((pyTy el).Contains "?") -> true
    | Some ta, Some tb when pyTy ta = pyTy tb -> false
    | _ -> match b.k with Arr _ | Iter _ | Call("range", _) | Call("slice", _) -> false | _ -> true

/// Array arithmetic broadcasts: a comprehension over the elements (zipped with an array on the right).
and private elementwise (op: string) (a: E) (b: E) =
    let sym = match op with "^^" -> "**" | "/" when elemTy (tyOf a) = Some TInt -> "//" | o -> o
    let p = match op with "+" | "-" -> 10 | "^^" -> 13 | _ -> 11
    let bIsArr = match tyOf b with Some(TArr _) -> true | _ -> false
    let u = alloc [ "p"; "u"; "a" ]
    let v = alloc [ "q"; "v"; "b" ]
    let text =
        if bIsArr then "[" + u + " " + sym + " " + v + " " + kw "for" + $" {u}, {v} " + kw "in" + " " + fnc "zip" + "(" + str a + ", " + str b + ")]"
        else "[" + u + " " + sym + " " + exP (p + 1) b + " " + kw "for" + $" {u} " + kw "in" + " " + str a + "]"
    taken.Remove u |> ignore
    taken.Remove v |> ignore
    text, 14

and private method (obj: E) (m: string) (args: E list) = exP 14 obj + "." + fnc m + "(" + list args + ")", 14
and private fcall (f: string) (args: E list) = fnc f + "(" + list args + ")", 14

and private call (e: E) (f: string) (args: E list) : string * int =
    match f, args with
    | "neg", [ x ] -> "-" + exP 12 x, 12
    | ("not" | "notbool"), [ x ] ->
        match x.k with
        | Call("has", [ c; k ]) -> exP 6 k + " " + kw "not in" + " " + exP 6 c, 5
        | Call("not", [ y ]) -> ex y
        | Bin(("=" | "!=" | "<" | ">" | "<=" | ">=") as o, p, q) ->
            let flip = match o with "=" -> "!=" | "!=" -> "=" | "<" -> ">=" | ">" -> "<=" | "<=" -> ">" | _ -> "<"
            ex { x with k = Bin(flip, p, q) }
        | _ when (match tyOf x with Some(TInt | TFloat) -> true | _ -> false) -> exP 6 x + " == " + num "0", 5
        | _ -> kw "not" + " " + exP 4 x, 4
    | "has", [ c; k ] -> exP 6 k + " " + kw "in" + " " + exP 6 c, 5
    | "slice", [ x; a; b ] ->
        let a' = match a.k with Int 0L -> "" | _ -> str a
        let b' =
            match b.k with
            | Call("len", [ y ]) when showE y = showE x -> ""
            | Bin("-", { k = Call("len", [ y ]) }, k) when showE y = showE x -> "-" + exP 12 k
            | _ -> str b
        exP 14 x + "[" + a' + ":" + b' + "]", 14
    | "range", [ a; b ] -> fnc "list" + "(" + range a b + ")", 14
    | "tuple", xs -> "(" + list xs + ")", 14
    | "repeat", [ x; n ] -> "[" + str x + "] * " + exP 12 n, 11
    | "mapof", [ { k = Arr ks }; { k = Arr vs } ] -> "{" + (List.map2 (fun k v -> str k + ": " + str v) ks vs |> String.concat ", ") + "}", 14
    | "set", [ { k = Arr [] } ] -> fnc "set" + "()", 14
    | "set", [ { k = Arr xs } ] -> "{" + list xs + "}", 14
    | ("Q" | "PQ" | "BT" | "G" | "DG"), [ { k = Arr xs } ] ->
        let name = match f with "Q" -> "Queue" | "PQ" -> "MinHeap" | "BT" -> "SortedSet" | "G" -> "Graph" | _ -> "DiGraph"
        fnc name + "(" + (if xs.IsEmpty then "" else "[" + list xs + "]") + ")", 14
    | "destructure", [ x; _ ] -> ex x
    | "pop", [ q ] -> method q "pop" []
    | "zero", [] -> num "0", 14
    | "memo", [ fn ] -> fnc "cache" + "(" + str fn + ")", 14
    | "entries", [ x ] -> ex x
    | "try", [] -> fnc "try" + "()", 14
    | f, [ { k = Fn _ } as fn ] when f.StartsWith negMark -> ex fn
    | f, args when f.StartsWith callMark || f.StartsWith nameMark || f.StartsWith negMark -> lib (f.Substring 1) args
    | _ -> lib f args

and private lib (f: string) (args: E list) : string * int =
    match tyOfVar f with
    | Some(TFn id) ->
        // a block that takes no argument is called as `f:0`
        let args = match Desugar.arity.TryGetValue id with | true, 0 -> [] | _ -> args
        fcall (if isFresh f then disp f else userName f) args
    | _ ->
    match f, args with
    | "println", _ -> fcall "print" args
    | "print", [ x ] -> fnc "print" + "(" + str x + ", end=" + strc "\"\"" + ")", 14
    | "read", [] -> fcall "input" []
    | "lines", [] -> fcall "read_lines" []
    | "sort", [ x ] -> fcall "sorted" [ x ]
    | "sort", [ x; c ] -> fnc "sorted" + "(" + str x + ", cmp=" + str c + ")", 14
    | "sortby", [ x; k ] -> fnc "sorted" + "(" + str x + ", key=" + str k + ")", 14
    | "rev", [ x ] -> fcall "reversed" [ x ]
    | "keys", [ m ] -> method m "keys" []
    | "vals", [ m ] -> method m "values" []
    | "items", [ m ] -> method m "items" []
    | "join", [ xs ] -> exP 14 (mk (Str "")) + "." + fnc "join" + "(" + gen xs + ")", 14
    | "join", [ xs; sep ] -> exP 14 sep + "." + fnc "join" + "(" + gen xs + ")", 14
    | ("sum" | "max" | "min" | "prod"), [ xs ] -> fnc f + "(" + gen xs + ")", 14
    | "split", [ s; sep ] -> method s "split" [ sep ]
    | ("lower" | "upper" | "isupper" | "islower" | "isdigit"), [ s ] -> method s f []
    | "ws", [ s ] -> method s "isspace" []
    | "trim", [ s ] -> method s "strip" []
    | "find", [ s; x ] -> method s "find" [ x ]
    | "replace", [ s; a; b ] -> method s "replace" [ a; b ]
    | "starts", [ s; x ] -> method s "startswith" [ x ]
    | "ends", [ s; x ] -> method s "endswith" [ x ]
    | "take", [ x; n ] -> exP 14 x + "[:" + str n + "]", 14
    | ("chars" | "arr"), [ x ] -> fcall "list" [ x ]
    | "uniq", [ x ] -> fcall "unique" [ x ]
    | "Q", _ -> fcall "Queue" args
    | "PQ", _ -> fcall "MinHeap" args
    | "G", _ -> fcall "Graph" args
    | "DG", _ -> fcall "DiGraph" args
    | "BT", [ x ] -> fcall "SortedSet" [ x ]
    | "BT", [ x; k ] -> fnc "SortedSet" + "(" + str x + ", key=" + str k + ")", 14
    | "map", _ -> fcall "dict" args
    | _ -> fcall (if isFresh f then disp f else userName f) args

/// The only argument of a call: a list comprehension reads as a generator there.
and private gen (e: E) =
    let s = str e
    match e.k with
    | Iter _ when s.StartsWith "[" && s.EndsWith "]" -> s.Substring(1, s.Length - 2)
    | _ -> s

and private range (a: E) (b: E) = exP 10 a + " ..< " + exP 10 b

/// `n ?{ arms }`: the arms test a subject variable; a variable subject is used directly.
and private matchAsCore (s: E) (t: string) (arms: E) (filter: bool) : E =
    let isArr = match tyOf s with Some(TArr _) -> true | _ -> false
    if isArr then
        mk (Iter { kind = (if filter then IFilter else IMap); src = s; el = t; ix = t + "ix"; acc = t + "acc"; body = [ Do arms ] })
    else
        match s.k with
        | Var _ -> subst t s arms
        | _ -> mk (Seq([ Assign(t, s) ], arms))

and private lambda (id: int) (body: S list) : string =
    let ss, v = splitBody body
    let n = max 1 (match Desugar.arity.TryGetValue id with | true, n -> n | _ -> 1)
    let ps = [ 1..n ] |> List.map (fun i -> $"_{i}")
    let saved = ps |> List.map (fun p -> p, (match names.TryGetValue p with | true, d -> Some d | _ -> None))
    let cands i = if n = 1 then [ "x"; "y"; "z" ] else [ [ "a" ]; [ "b" ]; [ "c" ]; [ "d" ] ].[min i 3]
    let shown = ps |> List.mapi (fun i p -> bind p (cands i))
    let text, hoisted = capture (fun () -> str v)
    let result =
        if ss.IsEmpty && hoisted.Count = 0 then kw "lambda" + " " + String.concat ", " shown + ": " + text
        else
            ps |> List.iter release
            let name = alloc [ "key"; "fn" ]
            defn curInd name id body false
            fnc name
    ps |> List.iter release
    for p, d in saved do
        match d with
        | Some d -> names.[p] <- d
        | None -> ()
    result

// ---------- iterations ----------

/// A block body as its statements (desugaring temps undone) and its value.
and private splitPrep (body: S list) =
    let ss, v = splitBody body
    let ss, v = prep ss (Some v)
    ss, v.Value

/// The fused chain of an iteration: its source and the stages, innermost (the one reading the source) first.
and private chain (it: Iter) : E * Iter list =
    match it.src.k with
    | Iter inner when (inner.kind = IMap || inner.kind = IFilter) && not (isSort inner) ->
        let s, st = chain inner
        s, st @ [ it ]
    | _ -> it.src, [ it ]

and private shapeOf (src: E) (st: Iter) =
    match src.k with
    | Call("range", [ a; b ]) -> "range", Some(a, b)
    | _ ->
        match tyOf src with
        | Some TInt -> "range", Some(mk (Int 0L), src)
        | Some(TMap _) -> "map", None
        | Some(TGraph _) -> "graph", None
        | Some TStr -> "str", None
        | Some(TSet _) -> "set", None
        | Some _ -> "plain", None
        | None ->
            match tyOfVar st.ix with
            | Some t when t <> TInt -> "map", None
            | _ -> "plain", None

/// Binds the loop variables of the first stage; returns the `for` header (without the colon).
and private header (src: E) (st: Iter) (stages: Iter list) =
    let bodies = stages |> List.collect (fun s -> s.body)
    let usedEl = countIn st.el bodies None > 0
    let usedIx = forceIx || countIn st.ix bodies None > 0
    forceIx <- false
    let shape, range' = shapeOf src st
    let singular =
        match src.k with
        | Var n when not (isFresh n) && (userName n).Length > 2 && (userName n).EndsWith "s" -> [ (userName n).TrimEnd('s') ]
        | Var n when not (isFresh n) && (userName n).Length = 2 && (userName n).EndsWith "s" -> [ (userName n).Substring(0, 1) ]
        | _ -> []
    let named n cands = match names.TryGetValue n with | true, d -> d | _ -> bind n cands
    let el cands = if usedEl then named st.el cands else "_"
    let ix cands = if usedIx then named st.ix cands else "_"
    let srcText () = str src
    match shape, range' with
    | "range", Some(a, b) ->
        let r = range a b
        if usedIx then
            let i = ix [ "idx" ]
            let x = el [ "i"; "j"; "k" ]
            kw "for" + $" {i}, {x} " + kw "in" + " " + fnc "enumerate" + "(" + r + ")"
        else kw "for" + " " + el [ "i"; "j"; "k"; "n" ] + " " + kw "in" + " " + r
    | ("map" | "graph"), _ ->
        let kc, vc = if shape = "graph" then [ "node"; "u" ], [ "nbrs" ] else [ "k"; "key" ], [ "v"; "val" ]
        let s = srcText ()
        match usedIx, usedEl with
        | true, true ->
            let k = ix kc
            let v = el vc
            kw "for" + $" {k}, {v} " + kw "in" + " " + exP 14 src + "." + fnc "items" + "()"
        | true, false -> kw "for" + " " + ix kc + " " + kw "in" + " " + s
        | false, _ -> kw "for" + " " + el vc + " " + kw "in" + " " + exP 14 src + "." + fnc "values" + "()"
    | _ ->
        let cands = singular @ (if shape = "str" then [ "ch"; "c" ] else [ "x"; "y"; "z"; "w"; "item" ])
        let s = srcText ()
        if usedIx && not usedEl then
            kw "for" + " " + ix [ "i"; "j"; "k" ] + " " + kw "in" + " " + num "0" + " ..< " + fnc "len" + "(" + s + ")"
        elif usedIx then
            let i = ix [ "i"; "j"; "k" ]
            let x = el cands
            kw "for" + $" {i}, {x} " + kw "in" + " " + fnc "enumerate" + "(" + s + ")"
        else kw "for" + " " + el cands + " " + kw "in" + " " + s

/// Emits the loop over a chain; `terminal ind stage` handles each element reaching the last stage.
and private loop ind (src: E) (stages: Iter list) (terminal: int -> Iter -> unit) =
    curInd <- ind
    let stages = Array.ofList (writeBack src stages)
    let bodies () = stages |> Seq.collect (fun s -> s.body) |> List.ofSeq
    let it0 = stages.[0]
    // `i = @` as the first statement: the loop variable takes the user's name
    match real it0.body with
    | Assign(u, { k = Var v }) :: _ when (v = it0.el || v = it0.ix) && not (isFresh u) && countIn ("=" + u) (bodies ()) None = 1
                                         && countIn ("=" + v) (bodies ()) None = 0 && not (names.ContainsKey v) ->
        let first = it0.body |> List.findIndex (function Assign(u', _) -> u' = u | _ -> false)
        let body = it0.body |> List.indexed |> List.filter (fst >> (<>) first) |> List.map snd
        stages.[0] <- { it0 with body = body }
        for j in 0 .. stages.Length - 1 do
            stages.[j] <- { stages.[j] with body = rwS (fun e -> match e.k with Var m when m = u -> Some(mk (Var v)) | _ -> None) stages.[j].body }
        names.[v] <- userName u
    | _ -> ()
    let it0 = stages.[0]
    let bound = ResizeArray<string>([ it0.el; it0.ix ])
    // a one-element literal source (`[x][ok]{...}`) is not a loop: the element is just named
    let single, inner =
        match src.k with
        | Arr [ one ] when countIn it0.ix (bodies ()) None = 0 ->
            match one.k with
            | Var w -> names.[it0.el] <- disp w
            | _ when countIn it0.el (bodies ()) None > 0 -> emit ind (bind it0.el [ "x"; "y"; "z" ] + " = " + str one)
            | _ -> if not (isPure one) then emit ind (str one)
            true, ind
        | _ ->
            let h = header src it0 (List.ofArray stages)
            emit ind (h + ":")
            false, ind + 1
    let rec go ind i =
        let st = stages.[i]
        if i = stages.Length - 1 then terminal ind st
        else
            let ss, v = splitPrep st.body
            emitAll ind ss
            let nxt = stages.[i + 1]
            bound.Add nxt.el
            match st.kind with
            | IFilter ->
                if names.ContainsKey st.el then names.[nxt.el] <- names.[st.el]
                curInd <- ind
                emit ind (kw "if" + " " + str (cond v) + ":")
                block (ind + 1) (fun () -> go (ind + 1) (i + 1))
            | _ ->
                let rest = stages |> Seq.skip (i + 1) |> Seq.collect (fun s -> s.body) |> List.ofSeq
                let uses = countIn nxt.el rest None
                // a filter or a seeded fold also uses its element implicitly (kept, or the fold's seed)
                let implicitUse =
                    match nxt.kind with
                    | IMap -> false
                    | IFold | IScan when i + 1 = stages.Length - 1 ->
                        (match foldOp nxt with Some o -> o = "max" || o = "min" || Option.isNone (identity o (tyOfVar nxt.el)) | None -> true)
                    | _ -> true
                if uses = 1 && simpleE v && not implicitUse then
                    // used once: the value goes straight into its use
                    pending.[nxt.el] <- v
                elif uses > 0 then
                    curInd <- ind
                    let value = str v
                    let y = bind nxt.el [ "y"; "z"; "v"; "val" ]
                    emit ind (y + " = " + value)
                elif not (isPure v) then stmtValue ind Discard v
                go ind (i + 1)
    if single then go inner 0 else block inner (fun () -> go inner 0)
    for st in stages do pending.Remove st.el |> ignore
    // a filter's alias shares its display name; release each display name once
    for n in bound do release n

and private simpleE (e: E) =
    match e.k with
    | Int _ | Float _ | Str _ | Bool _ | Var _ -> true
    | Bin(_, a, b) | Index(a, b) -> simpleE a && simpleE b
    | Call(("tuple" | "neg" | "len" | "not"), xs) | Arr xs -> List.forall simpleE xs
    | _ -> false

/// `m{_ = e}` on a map assigns the value and writes it back into m.
and private hasWriteBack (src: E) (stages: Iter list) =
    match stages with
    | [ st ] when (st.kind = IMap || st.kind = IEach) && fst (shapeOf src st) = "map" ->
        real st.body |> List.exists (function Do { k = Bin("=", { k = Var x }, _) } -> x = st.el | _ -> false)
    | _ -> false

and private writeBack (src: E) (stages: Iter list) =
    if not (hasWriteBack src stages) then stages
    else
        let st = stages.Head
        let body = real st.body
        let last = body.Length - 1
        let body =
            body |> List.indexed |> List.collect (fun (i, stmt) ->
                match stmt with
                | Do { k = Bin("=", { k = Var x }, e) } when x = st.el ->
                    [ Assign(x, e); SetIdx(src, mk (Var st.ix), mk (Var x)) ] @ (if i = last then [ Do(mk (Var x)) ] else [])
                | stmt -> [ stmt ])
        [ { st with body = body } ]

/// `acc op x` or `max/min(acc, x)` as the whole fold body.
and private foldOp (st: Iter) =
    match real st.body with
    | [ Do { k = Bin(op, { k = Var a }, { k = Var x }) } ] when a = st.acc && x = st.el -> Some op
    | [ Do { k = If({ k = Bin((">" | "<") as op, { k = Var a }, { k = Var x }) }, { k = Var a2 }, { k = Var x2 }) } ]
        when a = st.acc && x = st.el && a2 = a && x2 = x -> Some(if op = ">" then "max" else "min")
    | [ Do { k = Call(("'max" | "'min") as f, [ { k = Var a }; { k = Var x } ]) } ] when a = st.acc && x = st.el -> Some(f.Substring 1)
    | _ -> None

and private identity (op: string) (elTy: Ty option) =
    match op, elTy with
    | "+", Some TFloat -> Some(num "0.0")
    | "+", Some TStr -> Some(strc "\"\"")
    | "+", (Some TInt | None) -> Some(num "0")
    | "*", Some TFloat -> Some(num "1.0")
    | "*", (Some TInt | None) -> Some(num "1")
    | "|", Some TBool -> Some(num "False")
    | "&", Some TBool -> Some(num "True")
    | ("|" | "^"), _ -> Some(num "0")
    | "&", _ -> Some(num "-1")
    | _ -> None

/// A comprehension or builtin call for an iteration whose stages are single expressions; None when it needs a loop.
and private inlineIter (it: Iter) : string option =
    let src, stages = chain it
    let simple = stages |> List.forall (fun st -> (fst (splitPrep st.body)).IsEmpty)
    let filtersThenMap =
        stages |> List.indexed |> List.forall (fun (i, st) -> st.kind = IFilter || (st.kind = IMap && i = stages.Length - 1))
    let attempt (f: unit -> string) =
        let saved = Dictionary(names)
        let savedTaken = HashSet(taken)
        let text, hoisted = capture f
        // names bound while rendering are loop-local: forget them
        names.Clear()
        for kv in saved do names.[kv.Key] <- kv.Value
        taken.Clear()
        taken.UnionWith savedTaken
        if hoisted.Count = 0 then Some text else None
    if not simple || hasWriteBack src stages then None
    else
        match it.kind, foldOp it with
        | IFold, Some "+" when tyOfVar it.el = Some TStr ->
            // concatenating strings: a join
            match it.src.k with
            | Iter inner ->
                inlineIter inner |> Option.map (fun c ->
                    let gen = if c.StartsWith "[" && c.EndsWith "]" then c.Substring(1, c.Length - 2) else c
                    strc "\"\"" + "." + fnc "join" + "(" + gen + ")")
            | _ -> attempt (fun () -> strc "\"\"" + "." + fnc "join" + "(" + str src + ")")
        | IFold, Some op when stages.Length = 1 ->
            let elTy = tyOfVar it.el
            let name =
                match op, elTy with
                | "+", (Some(TInt | TFloat) | None) -> Some "sum"
                | "*", (Some(TInt | TFloat) | None) -> Some "prod"
                | ("max" | "min"), _ -> Some op
                | "|", Some TBool -> Some "any"
                | "&", Some TBool -> Some "all"
                | _ -> None
            name |> Option.bind (fun n -> attempt (fun () -> fnc n + "(" + str src + ")"))
        | IMap, _ when stages.Length = 1 && (match (snd (splitBody it.body)).k with Var x -> x = it.el | _ -> false) ->
            attempt (fun () -> fcall "list" [ src ] |> fst)
        | (IMap | IFilter), _ when filtersThenMap ->
            attempt (fun () ->
                let h = header src stages.Head stages
                let conds = ResizeArray<string>()
                let mutable value = ""
                for i, st in List.indexed stages do
                    let _, v = splitPrep st.body
                    if i + 1 < stages.Length && names.ContainsKey st.el then names.[stages.[i + 1].el] <- names.[st.el]
                    match st.kind with
                    | IFilter ->
                        conds.Add(exP 3 (cond v))
                        if i = stages.Length - 1 then value <- disp st.el
                    | _ -> value <- str v
                let cond = if conds.Count = 0 then "" else " " + kw "if" + " " + String.concat (" " + kw "and" + " ") conds
                let shape, _ = shapeOf src stages.Head
                match shape with
                | ("map" | "graph") when stages.Length = 1 ->
                    let k = match names.TryGetValue stages.Head.ix with | true, k -> k | _ -> bind stages.Head.ix [ "k"; "key" ]
                    let v = match names.TryGetValue stages.Head.el with | true, v -> v | _ -> bind stages.Head.el [ "v"; "val" ]
                    "{" + k + ": " + value + " " + kw "for" + $" {k}, {v} " + kw "in" + " " + exP 14 src + "." + fnc "items" + "()" + cond + "}"
                | "set" when stages.Length = 1 -> "{" + value + " " + h + cond + "}"
                | "str" when stages.Length = 1 && it.kind = IFilter -> strc "\"\"" + "." + fnc "join" + "(" + value + " " + h + cond + ")"
                | _ -> "[" + value + " " + h + cond + "]")
        | _ -> None

and private isSort (it: Iter) =
    match it.src.k with
    | Call("sort", [ { k = Iter inner } ]) -> (match inner.src.k with Call("entries", [ _ ]) -> true | _ -> false)
    | _ -> false

/// `xs${key}` desugars to a map over a sorted map of (key, index, element) tuples.
and private sortIter (it: Iter) : string option =
    match it.src.k, real it.body with
    | Call("sort", [ { k = Iter inner } ]), [ Do { k = Index({ k = Var el }, { k = Int 2L }) } ]
        when el = it.el && (match inner.src.k with Call("entries", [ _ ]) -> true | _ -> false) ->
        let src = match inner.src.k with Call("entries", [ s ]) -> s | _ -> inner.src
        let ss, v = splitBody inner.body
        let items = match tyOf src with Some(TMap _) -> fst (method src "items" []) | _ -> str src
        match v.k with
        | Call("tuple", [ { k = Var x }; _; _ ]) when ss.IsEmpty && x = inner.el -> Some(fnc "sorted" + "(" + items + ")")
        | Call("tuple", [ key; _; _ ]) ->
            let body = ss @ [ Do key ]
            let fn = mk (Fn(-1, rwS (fun e -> match e.k with Var n when n = inner.el -> Some(mk (Var "_1")) | _ -> None) body))
            Some(fnc "sorted" + "(" + items + ", key=" + str fn + ")")
        | _ -> None
    | _ -> None

/// An iteration used as a value: inline when it can be, otherwise a loop into a fresh variable (hoisted).
and private iterExpr (it: Iter) : string =
    match sortIter it with
    | Some s -> s
    | None ->
        match inlineIter it with
        | Some s -> s
        | None ->
            let name =
                match it.kind, foldOp it with
                | IFold, Some "+" ->
                    match tyOfVar it.el with
                    | Some(TInt | TFloat) | None -> [ "sum"; "total" ]
                    | Some TStr -> [ "text" ]
                    | _ -> [ "acc" ]
                | IFold, Some "*" -> [ "prod" ]
                | IFold, Some "max" -> [ "best" ]
                | IFold, Some "min" -> [ "least" ]
                | IScan, _ -> [ "running"; "prefix" ]
                | IFold, _ -> [ "acc" ]
                | _ -> [ "result" ]
            let target = alloc name
            iterInto curInd target None it
            target

/// Emits a loop that leaves the iteration's value in `target`.
and private iterInto ind (target: string) (comment: string option) (it: Iter) =
    let src, stages = chain it
    let term = List.last stages
    let shape, _ = shapeOf src stages.Head
    match it.kind with
    | IMap | IFilter ->
        let init =
            match shape with
            | ("map" | "graph") when stages.Length = 1 -> "{}"
            | "set" when stages.Length = 1 -> fnc "set" + "()"
            | _ -> "[]"
        emit ind (withComment (target + " = " + init) comment)
        let isDict = init = "{}"
        let isSet = init.Contains "set"
        forceIx <- isDict
        let push ind' (x: string) (st: Iter) =
            if isDict then emit ind' (target + "[" + disp st.ix + "] = " + x)
            elif isSet then emit ind' (target + "." + fnc "add" + "(" + x + ")")
            else emit ind' (target + "." + fnc "append" + "(" + x + ")")
        loop ind src stages (fun ind' st ->
            let ss, v = splitPrep st.body
            emitAll ind' ss
            curInd <- ind'
            match st.kind with
            | IFilter ->
                emit ind' (kw "if" + " " + str (cond v) + ":")
                push (ind' + 1) (disp st.el) st
            | _ -> push ind' (str v) st)
    | IFold | IScan ->
        let op = foldOp term
        let elTy = tyOfVar term.el
        let ident = op |> Option.bind (fun o -> identity o elTy)
        let scan = it.kind = IScan
        let acc = if scan then alloc [ "acc"; "run" ] else target
        names.[term.acc] <- acc
        if scan then emit ind (withComment (target + " = []") comment)
        emit ind (withComment (acc + " = " + defaultArg ident (kw "None")) (if scan then None else comment))
        loop ind src stages (fun ind' st ->
            let x = str (mk (Var st.el))
            match op, ident with
            | Some o, Some _ when o <> "max" && o <> "min" ->
                let sym = match o, elTy with
                          | "|", Some TBool -> None
                          | "&", Some TBool -> None
                          | _ -> Some(match o with "^^" -> "**" | "/" when elTy = Some TInt -> "//" | o -> o)
                match sym with
                | Some s -> emit ind' (acc + " " + s + "= " + x)
                | None -> emit ind' (acc + " = " + acc + " " + kw (if o = "|" then "or" else "and") + " " + x)
            | _ ->
                emit ind' (kw "if" + " " + acc + " " + kw "is None" + ":")
                emit (ind' + 1) (acc + " = " + x)
                emit ind' (kw "else" + ":")
                let ss, v = splitPrep st.body
                block (ind' + 1) (fun () ->
                    emitAll (ind' + 1) ss
                    stmtValue (ind' + 1) (To(acc, ref None, None)) v)
            if scan then emit ind' (target + "." + fnc "append" + "(" + acc + ")"))
        names.Remove term.acc |> ignore
    | IEach -> eachLoop ind it

/// An iteration run for its effects.
and private eachLoop ind (it: Iter) =
    let src, stages = chain it
    match sortIter it with
    | Some s ->
        emit ind s
    | None ->
        loop ind src stages (fun ind' st ->
            let ss, v = splitPrep st.body
            emitAll ind' ss
            if not (isPure v) then stmtValue ind' Discard v)

// ---------- statements ----------

and private defn ind (name: string) (id: int) (body: S list) (memo: bool) =
    let inst = match fnInst.TryGetValue id with | true, i -> Some i | _ -> None
    let n = match Desugar.arity.TryGetValue id with | true, n -> n | _ -> 0
    let ps = [ 1..n ] |> List.map (fun i -> $"_{i}")
    let saved = ps |> List.map (fun p -> p, (match names.TryGetValue p with | true, d -> Some d | _ -> None))
    // named parameters: the body starts with `p'id = _k`, and `p'id = _k@j` for the parts of an unpacked one
    let rec prologue (ss: S list) acc (groups: Map<string, (int64 * string) list>) =
        match ss with
        | Line _ :: rest -> prologue rest acc groups
        | Assign(p, { k = Var a }) :: rest when List.contains a ps && not (Map.containsKey a acc) && not (assignedIn a rest) ->
            prologue rest (Map.add a p acc) groups
        | Assign(p, { k = Index({ k = Var a }, { k = Int j }) }) :: rest when List.contains a ps && not (Map.containsKey a acc) ->
            prologue rest acc (Map.add a ((j, p) :: defaultArg (Map.tryFind a groups) []) groups)
        | _ -> acc, groups, ss
    let given, groups, body = prologue body Map.empty Map.empty
    let unpack =
        ps |> List.collect (fun a ->
            match Map.tryFind a groups with
            | None -> []
            | Some parts ->
                let parts = List.rev parts
                if parts |> List.mapi (fun i (j, _) -> int64 i = j) |> List.forall (fun ok -> ok) then
                    [ multi (parts |> List.map (fun (_, p) -> mk (Var p))) [ mk (Var a) ] ]
                else parts |> List.map (fun (j, p) -> Assign(p, mk (Index(mk (Var a), mk (Int j))))))
    let body = unpack @ body
    let parmTy i = inst |> Option.bind (fun i' -> List.tryItem i i'.parms) |> Option.map (snd >> resolve)
    let shown =
        ps |> List.mapi (fun i p ->
            match Map.tryFind p given with
            | Some u ->
                let d = userName u
                names.[p] <- d
                d
            | None ->
                let cands =
                    match parmTy i with
                    | Some(TArr _) -> [ "xs"; "ys"; "zs"; "items" ]
                    | Some TStr -> [ "s"; "t"; "text" ]
                    | Some TInt -> [ "n"; "m"; "k"; "num" ]
                    | Some(TMap _) -> [ "m"; "d"; "table" ]
                    | Some(TTup [ _; _ ]) -> [ "pair"; "p" ]
                    | Some(TTup [ _; _; _ ]) -> [ "triple"; "t" ]
                    | Some(TTup _) -> [ "p"; "parts" ]
                    | Some(TQ _ | TPQ _) -> [ "q"; "queue" ]
                    | _ -> [ "x"; "y"; "arg" ]
                bind p cands)
    let sigComment =
        inst |> Option.bind (fun i ->
            let s = (i.parms |> List.map (snd >> pyTy) |> String.concat ", ") + " -> " + pyTy i.ret
            if s.Contains "?" then None else Some s)
    if ind = 0 && out.Count > 0 && out.[out.Count - 1] <> "" then out.Add ""
    if memo then emit ind (fnc "@cache")
    emit ind (withComment (kw "def" + " " + defc name + "(" + String.concat ", " shown + "):") sigComment)
    block (ind + 1) (fun () -> returnBody (ind + 1) body)
    if ind = 0 then out.Add ""
    for p in ps do
        if not (Map.containsKey p given) then release p
        else names.Remove p |> ignore
    for p, d in saved do
        match d with
        | Some d -> names.[p] <- d
        | None -> ()

and private returnBody ind (body: S list) =
    let ss, v = splitBody body
    let ss, v = prep ss (Some v)
    emitAll ind ss
    if not ((real ss).Length > 0 && isZero v.Value) then stmtValue ind Return v.Value

and private stmts ind (ss: S list) = emitAll ind (fst (prep ss None))

/// Emits prepared statements; temporaries hoisted for one statement are free again after it.
and private emitAll ind (ss: S list) =
    for s in ss do
        let before = HashSet(taken)
        stmt ind s
        taken.IntersectWith before

and private compound (lhs: string) (lhsTy: Ty option) (op: string) (b: E) : string option =
    let m name = Some(lhs + "." + fnc name + "(" + str b + ")")
    match op, lhsTy with
    | "+", Some(TSet _ | TTree _) -> m "add"
    | "+", Some(TQ _ | TPQ _) -> m "push"
    | "-", Some(TSet _ | TTree _) -> m "remove"
    | "-", Some(TMap _) -> Some(kw "del" + " " + lhs + "[" + str b + "]")
    | "+", Some(TMap _) -> m "update"
    | "+", Some(TGraph _) -> m "add_edge"
    | "++", (Some(TArr _) | None) -> if appendsTy lhsTy b then m "append" else Some(lhs + " += " + str b)
    | ("+" | "-" | "*" | "/" | "%" | "^^"), Some(TArr _) -> None
    | "++", Some(TSet _) -> m "update"
    | "++", Some(TQ _ | TPQ _) -> m "push"
    | ("|" | "&"), _ when lhsTy = Some TBool || tyOf b = Some TBool ->
        Some(lhs + " = " + lhs + " " + kw (if op = "|" then "or" else "and") + " " + exP (if op = "|" then 3 else 4) b)
    | "**", Some TStr -> Some(lhs + " *= " + str b)
    | "+", Some TStr when (match tyOf b with Some TStr | None -> false | _ -> true) -> Some(lhs + " += " + fnc "str" + "(" + str b + ")")
    | ("+" | "-" | "*" | "%" | "^^" | "/" | "<<" | ">>" | ">>>" | "|" | "&" | "^" | "++"), _ ->
        let sym = match op with "^^" -> "**" | "/" when lhsTy = Some TInt -> "//" | "++" -> "+" | o -> o
        Some(lhs + " " + sym + "= " + str b)
    | _ -> None

and private appendsTy (lhsTy: Ty option) (b: E) =
    match lhsTy, tyOf b with
    | Some(TArr el), Some tb -> pyTy tb = pyTy el
    | _, None | None, _ -> (match b.k with Arr _ | Iter _ | Call("range", _) | Call("slice", _) -> false | _ -> true)
    | _ -> false

/// Structural equality up to the fresh names of iterations (`m@key` desugared twice from `m@key op= v`).
and private same (a: E) (b: E) =
    let norm (e: E) = Text.RegularExpressions.Regex.Replace(showE e, "[_@$]%[0-9]+", "%")
    norm a = norm b

and private stmt ind (s: S) =
    curInd <- ind
    match s with
    | Line _ -> ()
    | Assign(f, { k = Fn(id, body) }) -> defn ind (userName f) id body false
    | Assign(f, { k = Call(m, [ { k = Fn(id, body) } ]) }) when stripMark m = "memo" -> defn ind (userName f) id body true
    | Assign(x, { k = Bin(op, { k = Var x' }, b) }) when x = x' && op <> "??" && not (cmpOps.Contains op) && (compound (disp x) (tyOfVar x) op b).IsSome ->
        typed.Add x |> ignore
        emit ind (compound (disp x) (tyOfVar x) op b).Value
    | Assign(x, e) -> stmtValue ind (To(disp x, ref (typeComment x), Some x)) e
    | SetIdx(x, i, { k = Bin(op, { k = Index(x', i') }, b) }) when same x x' && same i i' && not (cmpOps.Contains op) ->
        let lhs = exP 14 x + "[" + str i + "]"
        match compound lhs (tyOf { k = Index(x, i); t = TUnk }) op b with
        | Some c -> emit ind c
        | None -> stmtValue ind (To(lhs, ref None, None)) { k = Bin(op, { k = Index(x', i'); t = TUnk }, b); t = TUnk }
    | SetIdx(x, i, v) ->
        let lhs = exP 14 x + "[" + str i + "]"
        stmtValue ind (To(lhs, ref None, None)) v
    | While(c, body) ->
        let text, hoisted = capture (fun () -> (curInd <- ind; str c))
        if hoisted.Count = 0 then
            emit ind (kw "while" + " " + text + ":")
            block (ind + 1) (fun () -> stmts (ind + 1) body)
        else
            emit ind (kw "while" + " " + num "True" + ":")
            curInd <- ind + 1
            let cond = exP 4 c
            emit (ind + 1) (kw "if not" + " " + cond + ": " + kw "break")
            stmts (ind + 1) body
    | Do { k = Call("%multi", [ { k = Arr lhs }; { k = Arr values } ]) } ->
        let lhsText = lhs |> List.map (fun l -> match l.k with Var n -> disp n | _ -> str l)
        lhs |> List.iter (fun l -> match l.k with Var n -> typed.Add n |> ignore | _ -> ())
        match values with
        | [ v ] ->
            // `a, b = c ? [x, y] : [z, w]`: the branches read as tuples
            let rec tuples (e: E) =
                match e.k with
                | Arr xs when xs.Length = lhs.Length -> { e with k = Call("tuple", xs) }
                | If(c, a, b) -> { e with k = If(c, tuples a, tuples b) }
                | _ -> e
            stmtValue ind (Multi lhsText) (tuples v)
        | vs -> emit ind (String.concat ", " lhsText + " = " + list vs)
    | Do e -> stmtValue ind Discard e

and private sinkEmit ind (sink: Sink) (e: E) =
    curInd <- ind
    match sink with
    | Discard when isPure e -> ()
    | Discard -> emit ind (str e)
    | Return -> emit ind (kw "return" + " " + str e)
    | To(lhs, c, x) ->
        let comp =
            match x, e.k with
            | Some x, Bin(op, { k = Var x' }, b) when x = x' && op <> "??" && not (cmpOps.Contains op) -> compound lhs (tyOfVar x) op b
            | _ -> None
        match comp with
        | Some s -> emit ind s
        | None ->
            let s = str e
            emit ind (withComment (lhs + " = " + s) c.Value)
            c.Value <- None
    | Multi lhs ->
        let rhs =
            match e.k with
            | Arr xs | Call("tuple", xs) when xs.Length = lhs.Length -> list xs
            | _ -> str e
        emit ind (String.concat ", " lhs + " = " + rhs)

/// Emits `e` as statements whose value goes to `sink`: conditionals become if/elif/else, iterations loops.
and private stmtValue ind (sink: Sink) (e: E) =
    curInd <- ind
    let branchy (x: E) = match x.k with Seq _ | If _ -> true | Iter _ -> (match sink with Discard -> true | _ -> false) | _ -> false
    match e.k with
    | Seq(ss, x) ->
        let ss, x = prep ss (Some x)
        emitAll ind ss
        stmtValue ind sink x.Value
    | If({ k = Call("try", []) }, { k = Seq(ss, _) }, _) ->
        emit ind (kw "try" + ":")
        block (ind + 1) (fun () -> stmts (ind + 1) ss)
        emit ind (kw "except" + " " + fnc "RuntimeError" + ": " + kw "pass")
    | If(c, a, b) ->
        let inlineOk () =
            match sink with
            | Discard -> false
            | _ when branchy a || branchy b -> false
            | _ ->
                let text, hoisted = capture (fun () -> (curInd <- ind; probing <- true; try str e finally probing <- false))
                hoisted.Count = 0 && visibleLen text <= 64
        if inlineOk () then sinkEmit ind sink e
        else
            let a, b, c = if sink = Discard && isZero a && not (isZero b) then b, a, mk (Call("not", [ c ])) else a, b, c
            curInd <- ind
            emit ind (kw "if" + " " + str (cond c) + ":")
            block (ind + 1) (fun () -> stmtValue (ind + 1) sink a)
            let rec elseBranch (b: E) =
                match sink, b.k with
                | Return, _ -> stmtValue ind Return b
                | Discard, _ when isZero b -> ()
                | _, If(c2, a2, b2) when (match c2.k with Call("try", []) -> false | _ -> true) ->
                    curInd <- ind
                    emit ind (kw "elif" + " " + str (cond c2) + ":")
                    block (ind + 1) (fun () -> stmtValue (ind + 1) sink a2)
                    elseBranch b2
                | _ ->
                    emit ind (kw "else" + ":")
                    block (ind + 1) (fun () -> stmtValue (ind + 1) sink b)
            elseBranch b
    | Iter it when sink = Discard -> eachLoop ind it
    | Iter it ->
        match sink with
        | To(lhs, c, _) when not (lhs.Contains "[") && inlineIter it = None && sortIter it = None
                          && not (names |> Seq.exists (fun kv -> kv.Value = lhs && occurs kv.Key e)) && not (occurs lhs e) ->
            iterInto ind lhs c.Value it
            c.Value <- None
        | _ -> sinkEmit ind sink e
    | MatchOn(s, t, arms, filter) -> stmtValue ind sink (matchAsCore s t arms filter)
    | _ -> sinkEmit ind sink e

/// The pseudocode for a desugared program, with types from its typed form.
let explain (core: S list) (prog: Typer.Program) (useColor: bool) : string =
    color <- useColor
    varTy.Clear(); fnInst.Clear(); names.Clear(); taken.Clear(); reserved.Clear(); typed.Clear()
    out <- ResizeArray()
    curInd <- 0
    for kv in prog.varTy do
        let k = kv.Key
        let dot = k.LastIndexOf '.'
        let b = if dot > 0 && k.Substring(dot + 1) |> Seq.forall Char.IsDigit then k.Substring(0, dot) else k
        // one source name in several scopes (or instances) with different types: no type is better than a wrong one
        match varTy.TryGetValue b with
        | true, t when pyTy t <> pyTy kv.Value -> varTy.[b] <- TUnk
        | true, _ -> ()
        | _ -> varTy.[b] <- kv.Value
    for i in prog.insts do
        match i.name.TrimStart('f').Split('_') with
        | [| id; _ |] ->
            match Int32.TryParse id with
            | true, id when not (fnInst.ContainsKey id) -> fnInst.[id] <- i
            | _ -> ()
        | _ -> ()
    // every user-visible name is reserved, so loop variables never shadow one
    let reserve (e: E) =
        match e.k with
        | Var n when n.StartsWith "=" -> (let n = n.Substring 1 in if not (isFresh n) then reserved.Add(userName n) |> ignore)
        | Var n when not (isFresh n) -> reserved.Add(userName n) |> ignore
        | Call(f, _) -> reserved.Add(userName (stripMark f)) |> ignore
        | _ -> ()
    visitS reserve core
    stmts 0 core
    let lines = out |> Seq.toList
    // no blank line at either end, nor two in a row
    let lines = lines |> List.fold (fun acc l -> match acc with "" :: _ when l = "" -> acc | _ -> l :: acc) [] |> List.rev
    let lines = lines |> List.skipWhile ((=) "") |> List.rev |> List.skipWhile ((=) "") |> List.rev
    String.concat "\n" lines
