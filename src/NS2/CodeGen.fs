module NS2.CodeGen

open System
open System.Text
open NS2.Ast
open System.Collections.Generic
open NS2.Type

type CodegenState =
    {
      mutable Reg: int
      mutable Label: int
      Vars: Dictionary<string, string>
      Code: StringBuilder
      StringConstants: StringBuilder
    }

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
    
let emit (st: CodegenState) (line: string) = st.Code.AppendLine(line) |> ignore

let TypeToLLVM =
    function
    | StringType -> "i8*"
    | IntType -> "i32"
    | BoolType -> "i1"
    | _ -> failwith "type unknown"

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
        
    | Block exprs ->
        let mutable last = ""
        for e in exprs do
            last <- codegen_expr state e
        last
    
    | Int n -> n.ToString()
    | String s ->
        let const_name = nextSpecialLabel state "@str"
        let line = $"{const_name} = private unnamed_addr constant [{s.Length+1} x i8] c\"{s}\00\", align 1"
        
        state.StringConstants.AppendLine line |> ignore
        let tmp = nextReg state
        emit state $"{tmp} = getelementptr [{s.Length+1} x i8], [{s.Length+1} x i8]* {const_name}, i32 0, i32 0"
        $"i8* {tmp}"

    | Id name ->
       let typ = TypeToLLVM t
       let tmp = nextReg state
       emit state $"{tmp} = load {typ}, {typ}* %%{name}, align 4"    
       $"{typ} {tmp}"

    | Assign (Id name, Typed(node,ty)) ->
        
        let rhs = codegen_expr state (Typed(node,ty))
        let isInt = match node with | Int _ -> true | _ -> false
        
        match ty with
        | StringType ->
            emit state $"%%{name} = alloca i8*, align 8"
            emit state $"store {rhs}, i8** %%{name}, align 8"
        | IntType when isInt -> 
            emit state $"%%{name} = alloca i32, align 4"
            emit state $"store i32 {rhs}, i32* %%{name}, align 4"
        | IntType -> 
            emit state $"%%{name} = alloca i32, align 4"
            emit state $"store {rhs}, i32* %%{name}, align 4"
        ""

    | Binop (left, op, right) ->
        let typ = TypeToLLVM t
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

    | IfPhi (cond, thenBranch, Some elseBranch, phis) ->
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
        for Typed (Phi(var, thenvar, elsevar),t) in phis do
            let typ = TypeToLLVM t
            emit state $"%%{var} = phi {typ}* [%%{thenvar}, %%{thenLabel}], [%%{elsevar}, %%{elseLabel}]"
        ""
    
    | Call (id, args) ->
        let targs = args |> List.map (codegen_expr state)
        let code_args = String.concat ", " targs
        match t with
        | VoidType ->
            emit state $"call void @{id} ({code_args})"
            ""
        | _ ->
            let ret_typ = TypeToLLVM t
            let tmp = nextReg state
            emit state $"{tmp} = call {ret_typ} @{id} ({code_args})"
            tmp
            
    | x -> $"Unsupported %s{x.GetType().Name}"

let codegen (program: AST) : string =
    let state =
        {
          Reg = 1
          Label = 0
          Vars = Dictionary()
          Code = StringBuilder()
          StringConstants = StringBuilder()
        }

    
    emit state "define i32 @main() {"
    let result_reg = codegen_expr state program
    emit state $"ret i32 0"
    emit state "}"

    state.StringConstants.ToString() + "\n" + state.Code.ToString()
