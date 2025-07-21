module NS2.CodeGen

open NS2.Ast
open System.Collections.Generic
open NS2.Type

type CodegenState =
    { mutable Reg: int
      mutable Label: int
      Vars: Dictionary<string, string>
      mutable Code: string list
      mutable StringConstants: string list }

let nextReg (st: CodegenState) =
    let r = st.Reg
    st.Reg <- st.Reg + 1
    $"%%{r}"

let nextLabel (st: CodegenState) =
    let l = st.Label
    st.Label <- st.Label + 1
    $"label{l}"

let nextSpecialLabel (st: CodegenState) (label:string)=
    let l = st.Label
    st.Label <- st.Label + 1
    $"{label}{l}"
let emit (st: CodegenState) (line: string) =
    st.Code <- st.Code @ [line]

let rec codegen_expr (state: CodegenState) (ast: AST) : string =
    
    let node, t =
        match ast with
        | Typed (x,t) -> (x,t)
        | x -> (x,AnyType)

    match node with
    | Root block ->
        let mutable last = ""
        for e in block do
            last <- codegen_expr state e
        last
        
    | Int n -> n.ToString()
    | String s ->
        let const_name = nextSpecialLabel state "@str"
        let line = $"{const_name} = private unnamed_addr constant [{s.Length+1} x i8] c\"{s}\00\", align 1"
        
        state.StringConstants <- line::state.StringConstants
        let tmp = nextReg state
        emit state $"{tmp} = getelementptr [{s.Length+1} x i8], [{s.Length+1} x i8]* {const_name}, i32 0, i32 0"
        $"i8* {tmp}"

    | Id name ->
       let typ =
            match t with
            | StringType -> "i8*"
            | IntType -> "i32"
            | _ -> failwith $"CodeGen: type not supported {t}"
            
       let tmp = nextReg state
       emit state $"{tmp} = load {typ}, {typ}* %%{name}, align 4"    
       $"{typ} {tmp}"

    | Assign (Id name, Typed(node,ty)) ->
        
        let rhs = codegen_expr state (Typed(node,ty))
        
        match ty with
        | StringType ->
            emit state $"%%{name} = alloca i8*, align 8"
            emit state $"store {rhs}, i8** %%{name}, align 8"
        | IntType -> 
            emit state $"%%{name} = alloca i32, align 4"
            emit state $"store {rhs}, i32* %%{name}, align 4"
        ""

    | Binop (left, op, right) ->
        let typ =
            match t with
            | StringType -> "i8*"
            | IntType -> "i32"
            | BoolType -> "i1"
            | _ -> failwith $"CodeGen: Binop type not supported {t}"
            
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
        emit state $"{result} = {op_instr} {l}, {r}"
        $"{typ} {result}"

    | If (cond, thenBranch, Some elseBranch) ->
        let cond_reg = codegen_expr state cond
        let zero = nextReg state
        emit state $"{zero} = icmp ne {cond_reg}, 0"

        let thenLabel = nextSpecialLabel state "then"
        let elseLabel = nextSpecialLabel state "else"
        let endLabel = nextSpecialLabel state "endif"

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
        { Reg = 1
          Label = 0
          Vars = Dictionary()
          Code = []
          StringConstants = []
          }

    
    emit state "define i32 @main() {"
    let result_reg = codegen_expr state program
    emit state $"ret i32 {result_reg}"
    emit state "}"

    (String.concat "\n" (List.rev state.StringConstants)) + "\n" + String.concat "\n" state.Code
