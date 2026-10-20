/// Stage 5: representation. Decides, by rule, how every type is laid out: its LLVM type in registers, its
/// storage inside containers and tuples (size and LLVM type), and its runtime descriptor. Codegen and the
/// runtime (through descriptors) follow these decisions; nothing else hard-codes a layout.
module Repr

open Core

/// Scalars have no heap object. An unbound hole defaults to int.
let isScalar t = match resolve t with TInt | TFloat | TBool | THole _ | TUnk -> true | _ -> false

/// A tuple of scalars is a value: an LLVM struct in registers, stored inline (8 bytes per member), never boxed.
let isValueTuple t = match resolve t with TTup ts -> List.forall isScalar ts | _ -> false

/// Runtime descriptor: I F S B, A<t>, M<k><v>, H<t>, Q<t>, P<t>, G<t>, T<t>, K<k><t> (tree keyed by k), U<n>.. (boxed tuple), V<n>.. (value tuple).
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
    | TTree(e, None) -> "T" + desc e
    | TTree(e, Some(_, k)) -> "K" + desc k + desc e
    | TGraph e -> "G" + desc e
    | TTup ts -> (if isValueTuple t then "V" else "U") + string ts.Length + (ts |> List.map desc |> String.concat "")
    | _ -> "I"

/// A value-tuple member is stored in 8 bytes; a bool member as i64 0/1.
let private memberTy t = match resolve t with TFloat -> "double" | _ -> "i64"

/// LLVM type of a value in registers / locals.
let llty t =
    match resolve t with
    | TInt | TFn _ | THole _ | TUnk -> "i64"
    | TFloat -> "double"
    | TBool -> "i1"
    | TTup ts when isValueTuple t -> "{ " + (ts |> List.map memberTy |> String.concat ", ") + " }"
    | _ -> "ptr"

/// LLVM type of a value's storage inside a container or tuple (bool is one byte).
let storage t = match resolve t with TBool -> "i8" | _ -> llty t

/// Storage size in bytes (the runtime derives the same number from the descriptor).
let esz t = match resolve t with TBool -> 1 | TTup ts when isValueTuple t -> 8 * ts.Length | _ -> 8

/// Byte offset of member k inside a boxed tuple's object (members stored back to back, `esz` each).
let tupOffset t k = match resolve t with TTup ts -> ts |> List.take k |> List.sumBy esz | _ -> failwith "tupOffset: not a tuple"

/// Refcounted heap object?
let isPtr t = llty t = "ptr"

/// `ns repr`: the layout chosen for every type the program uses.
let describe (prog: Typer.Program) =
    let rec parts t =
        t :: (match resolve t with
              | TArr e | TSet e | TQ e | TPQ e | TGraph e | TTree(e, None) -> parts e
              | TTree(e, Some(_, k)) -> parts k @ parts e
              | TMap(k, v) -> parts k @ parts v
              | TTup ts -> List.collect parts ts
              | _ -> [])
    prog.varTy.Values
    |> Seq.collect parts
    |> Seq.filter (fun t -> match resolve t with TFn _ | TUnk -> false | _ -> true)
    |> Seq.distinctBy showTy
    |> Seq.map (fun t ->
        $"{showTy t,-24} register {llty t,-20} storage {storage t,-20} {esz t,2} bytes  desc {desc t}")
    |> String.concat "\n"
