module NS2.Print

open NS2.Ast

let rec printAst (indentSize: int) (ast: AST) =
    let rec print (level: int) (node: AST) =
        let indent = String.replicate (level * indentSize) "-"
        let newline = "\n"

        let printList tag items =
            indent + tag + newline +
            (items |> List.map (print (level + 1)) |> String.concat "")

        match node with
        | Root lst -> printList "Root" lst
        | Block lst -> printList "Block" lst
        | Int i -> indent + $"Int {i}\n"
        | String s -> indent + $"String \"{s}\"\n"
        | Bool b -> indent + $"Bool {b}\n"
        | Id s -> indent + $"Id {s}\n"
        | Binop (lhs, op, rhs) ->
            indent + $"Binop '{op}'\n" +
            print (level + 1) lhs +
            print (level + 1) rhs
        | Unop (op, value) ->
            indent + $"Unop '{op}'\n" +
            print (level + 1) value
        | Index (arr, idx) ->
            indent + "Index\n" +
            print (level + 1) arr +
            print (level + 1) idx
        | Array items -> printList "Array" items
        | Call (name, args) ->
            indent + $"Call '{name}'\n" +
            (args |> List.map (print (level + 1)) |> String.concat "")
        | FuncCalled (args, body) ->
            indent + "FuncCalled\n" +
            (args |> List.map (print (level + 1)) |> String.concat "") +
            print (level + 1) body
        | Func (name, body) ->
            indent + $"Func '{name}'\n" +
            print (level + 1) body
        | Map (key, value) ->
            indent + "Map\n" +
            print (level + 1) key +
            print (level + 1) value
        | Assign (lhs, rhs) ->
            indent + "Assign\n" +
            print (level + 1) lhs +
            print (level + 1) rhs
        | Pipe items -> printList "Pipe" items
        | If (cond, thenBranch, elseOpt) ->
            indent + "If\n" +
            print (level + 1) cond +
            print (level + 1) thenBranch +
            (match elseOpt with
             | Some e -> print (level + 1) e
             | None -> indent + String.replicate indentSize " " + "Else: None\n")
        | While (cond, body) ->
            indent + "While\n" +
            print (level + 1) cond +
            print (level + 1) body
        | WhilePhi (condphi, cond, body, bodyphi) ->
            indent + "WhilePhi\n" +
            print (level + 1) cond +
            print (level + 1) body +
            printList "condphi:" condphi +
            printList "bodyphi:" bodyphi
        | Typed (expr, typ) ->
            indent + $"Typed ({typ})\n" +
            print (level + 1) expr
        | Nop -> indent + "Nop\n"
        | IfPhi(cond, thenBranch, elseOpt, phis) ->
            indent + "IfPhi\n" +
            print (level + 1) cond +
            print (level + 1) thenBranch +
            (match elseOpt with
             | Some e -> print (level + 1) e
             | None -> indent + String.replicate indentSize " " + "Else: None\n") +
            printList "Phis:" phis
        | Phi(s, s1, s2) -> indent + $"Phi({s},{s1},{s2})\n"
        | PhiSingle(s, s1, s2) -> indent + $"PhiSingle({s},{s1},{s2})\n"
        | x -> failwith $"print not recognized: %A{x}"

    print 0 ast
