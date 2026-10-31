/// Queues: `Q[..]` (FIFO) and `PQ[..]` (priority, smallest first). Iterating one drains it (pop until empty), so
/// the loop body may push more work (a worklist); over a PQ of tuples `@` is the priority and `_` the rest.
/// `q@0` peeks; other indices copy to an array.
module Features.Queue

open Core
open Typer

let private v n = mk (Var n)
let private pop s = mk (Call("pop", [ v s ]))

let private isQueue t = match resolve t with TCon(n, _) -> Lower.drains.ContainsKey n | _ -> false

let private index (x: E) (i: E) =
    match resolve x.t with
    | TCon(_, [ el ]) when isQueue x.t ->
        match i.k with
        | Int 0L -> Some(typed (Call("peek", [ x ])) el)
        | _ -> Some(typed (Index(stdCall "arr" [ x ], conv i TInt)) el)
    | _ -> None

let private queue (name: string) (ctorC: string) (desc: string) (load: Ty -> string -> string -> string -> string -> string -> S list) =
    let coll = Coll.collection name ctorC
    { coll with
        lex = [ Coll.literalOpener name ]
        prefix = [ name + "[", Coll.literal name ]
        types = [ { con = name; show = (fun s ts -> name + " " + s ts.Head); desc = fun d ts -> desc + d ts.Head } ]
        init = coll.init @ [ fun () -> Lower.drains.[name] <- load ] }

/// Q: `@` counts the elements popped so far.
let q = queue "Q" "ns_q_new" "Q" (fun _ s _ el ix c -> [ Assign(el, pop s); Assign(ix, v c) ])

/// PQ of tuples: `@` = the priority (first value), `_` = the rest; otherwise `@` is the element itself.
let pq =
    queue "PQ" "ns_pq_new" "P" (fun srcTy s t el ix _ ->
        match srcTy with
        | TCon(_, [ e ]) ->
            match resolve e with
            | TTup(_ :: rest) ->
                let get i = mk (Index(v t, mk (Int(int64 i))))
                let value = match rest with [ _ ] -> get 1 | _ -> mk (Call("tuple", rest |> List.mapi (fun i _ -> get (i + 1))))
                [ Assign(t, pop s); Assign(ix, get 0); Assign(el, value) ]
            | _ -> [ Assign(el, pop s); Assign(ix, v el) ]
        | _ -> failwith "PQ: not a queue")

/// What both share: peeking.
let feature =
    { Feature.empty with
        name = "queues"
        index = [ index ]
        freshness = [ "peek", fun _ -> false ] // the front element is shared with the queue
        effects = [ "peek", Feature.Partial ]
        llvm =
          [ "peek", fun g e args vs ->
                let out = g.outCell e.t
                g.rt "void" "ns_peek" [ g.desc args.Head.t; "ptr", vs.[0]; "ptr", out ] |> ignore
                g.loadFrom out e.t ] }
