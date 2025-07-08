module NS2.CodeGen

open NS2.Ast
open System.Collections.Generic

type CodegenState =
    { mutable Reg: int
      mutable Label: int
      Vars: Dictionary<string, string>
      mutable Code: string list }

let nextReg (st: CodegenState) =
    let r = st.Reg
    st.Reg <- st.Reg + 1
    $"%%{r}"

let nextLabel (st: CodegenState) =
    let l = st.Label
    st.Label <- st.Label + 1
    $"label{l}"

let emit (st: CodegenState) (line: string) =
    st.Code <- st.Code @ [line]

let rec codegen_expr (state: CodegenState) (ast: AST) : string =
    match ast with
    | Root block ->
        let mutable last = ""
        for e in block do
            last <- codegen_expr state e
        last
        
    | Int n ->
        let r = nextReg state
        emit state $"{r} = add i32 0, {n}"
        r

    | Id name ->
        if state.Vars.ContainsKey(name) then
            let reg = nextReg state
            emit state $"{reg} = load i32, i32* {state.Vars.[name]}"
            reg
        else
            failwith $"Undefined variable: {name}"

    | Assign (Id name, rhs) ->
        let rhs_reg = codegen_expr state rhs
        let ptr = $"%%{name}"
        if not (state.Vars.ContainsKey(name)) then
            emit state $"{ptr} = alloca i32"
            state.Vars.[name] <- ptr
        emit state $"store i32 {rhs_reg}, i32* {ptr}"
        rhs_reg

    | Binop (left, op, right) ->
        let l = codegen_expr state left
        let r = codegen_expr state right
        let result = nextReg state
        let op_instr =
            match op with
            | "+" -> "add"
            | "-" -> "sub"
            | "*" -> "mul"
            | "/" -> "sdiv"
            | "%" -> "srem"
            | "==" -> "icmp eq"
            | "!=" -> "icmp ne"
            | "<" -> "icmp slt"
            | "<=" -> "icmp sle"
            | ">" -> "icmp sgt"
            | ">=" -> "icmp sge"
            | _ -> failwith $"Unsupported op: {op}"
        emit state $"{result} = {op_instr} i32 {l}, {r}"
        result

    | If (cond, thenBranch, Some elseBranch) ->
        let cond_reg = codegen_expr state cond
        let zero = nextReg state
        emit state $"{zero} = icmp ne i32 {cond_reg}, 0"

        let thenLabel = nextLabel state
        let elseLabel = nextLabel state
        let endLabel = nextLabel state

        emit state $"br i1 {zero}, label %%{thenLabel}, label %%{elseLabel}"
        emit state $"{thenLabel}:"
        let then_val = codegen_expr state thenBranch
        emit state $"br label %%{endLabel}"

        emit state $"{elseLabel}:"
        let else_val = codegen_expr state elseBranch
        emit state $"br label %%{endLabel}"

        emit state $"{endLabel}:"
        let phi = nextReg state
        emit state $"{phi} = phi i32 [{then_val}, %%{thenLabel}], [{else_val}, %%{elseLabel}]"
        phi

    | Block exprs ->
        let mutable last = ""
        for e in exprs do
            last <- codegen_expr state e
        last

    | _ -> failwith $"Unsupported node in codegen: {ast}"

let codegen (program: AST) : string =
    let state =
        { Reg = 0
          Label = 0
          Vars = Dictionary()
          Code = [] }

    emit state "define i32 @main() {"
    let result_reg = codegen_expr state program
    emit state $"ret i32 {result_reg}"
    emit state "}"

    String.concat "\n" state.Code
