module NS2.CodeGen

open System
open System.Diagnostics.Metrics
open System.Text
open NS2.Ast
open System.Collections.Generic
open NS2.Type

type CodegenState =
    {
      Counters: Dictionary<string, int>
      Code: StringBuilder
      StringConstants: StringBuilder
    }

let nextReg (st: CodegenState) =
    let r = st.Counters.GetValueOrDefault("reg",0)+1
    st.Counters["reg"] <- r
    $"%%{r}"

let nextLabel (st: CodegenState) (label:string)=
    let r = st.Counters.GetValueOrDefault(label,0)+1
    st.Counters[label] <- r
    $"{label}{r}"
    
let typeof =
    function
    | Typed(_,t) -> t
    | _ -> failwith "typeof node not Typed"
    
let emit (st: CodegenState) (line: string) = st.Code.AppendLine(line) |> ignore

let TypeToLLVM =
    function
    | StringType -> "i8*"
    | IntType -> "i32"
    | BoolType -> "i1"
    | x -> failwith $"TypeToLLVM: type unknown {x}"

let codegen_op =
    function
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
    | op -> failwith $"Unsupported op: {op}"
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
        let const_name = nextLabel state "@str"
        let line = $"{const_name} = private unnamed_addr constant [{s.Length+1} x i8] c\"{s}\00\", align 1"
        
        state.StringConstants.AppendLine line |> ignore
        let tmp = nextReg state
        emit state $"{tmp} = getelementptr [{s.Length+1} x i8], [{s.Length+1} x i8]* {const_name}, i32 0, i32 0"
        tmp

    | Id name -> $"%%{name}"
    | PtrId name ->
       let typ = TypeToLLVM t
       let tmp = nextReg state
       emit state $"{tmp} = load {typ}, {typ}* %%{name}, align 4"    
       tmp
       
    | Assign (Id name, Typed(Int i,_)) ->
        emit state $"%%{name} = add i32 {i}, 0"   
        ""
        
    | Assign (PtrId name, Typed(Int i,_)) ->
        let typ = TypeToLLVM t
        emit state $"%%{name} = load {typ}, {typ}* %%{name}, align 4"    
        ""
        
    | Assign(Id name, Typed(Binop (left, op, right), typ)) ->
        let l = codegen_expr state left
        let r = codegen_expr state right
        let op_instr = codegen_op op
        emit state $"%%{name} = {op_instr} {TypeToLLVM typ} {l}, {r}"
        ""
        
    | Assign (Id name, x) ->
        failwith "codegen handle assign explicit"
        
    | Binop (left, op, right) ->
        let typ = left |> typeof |> TypeToLLVM
        let l = codegen_expr state left
        let r = codegen_expr state right
        let result = nextReg state
        let op_instr = codegen_op op
        emit state $"{result} = {op_instr} {typ} {l}, {r}"
        result

    | IfPhi (cond, thenBranch, Some elseBranch, phis) ->
        let cond_reg = codegen_expr state cond

        let thenLabel = nextLabel state "then"
        let elseLabel = nextLabel state "else"
        let endLabel = nextLabel state "endif"

        emit state $"br i1 {cond_reg}, label %%{thenLabel}, label %%{elseLabel}"
        emit state $"{thenLabel}:"
        let then_val = codegen_expr state thenBranch
        emit state $"br label %%{endLabel}"

        emit state $"{elseLabel}:"
        let else_val = codegen_expr state elseBranch
        emit state $"br label %%{endLabel}"

        emit state $"{endLabel}:"
        for p in phis do
            match p with
            | Typed (Phi(var, thenvar, elsevar),t) -> 
                let typ = TypeToLLVM t
                emit state $"%%{var} = phi {typ} [%%{thenvar}, %%{thenLabel}], [%%{elsevar}, %%{elseLabel}]"
            | x -> codegen_expr state x |> ignore
            
        ""
    
    | WhilePhi(c, b, pre_assign) ->
        let entryLabel = nextLabel state "entry"
        let condLabel = nextLabel state "cond"
        let loopLabel = nextLabel state "loop"
        let exitLabel = nextLabel state "exit"

        emit state $"br label %%{entryLabel}"
        emit state $"{entryLabel}:"
        
        for x in pre_assign do
            codegen_expr state x
        
        emit state $"br label %%{condLabel}"

        emit state $"{condLabel}:"
        let cond_reg = codegen_expr state c
        emit state $"br i1 {cond_reg}, label %%{loopLabel}, label %%{exitLabel}"

        emit state $"{loopLabel}:"
        codegen_expr state b
        emit state $"br label %%{condLabel}"
        emit state $"{exitLabel}:"
        ""
        
    | CreatePtr (Id id, ty) -> 
        match ty with
        | StringType -> emit state $"%%{id} = alloca i8*, align 8"
        | IntType ->  emit state $"%%{id} = alloca i32, align 4"
        ""
      
    (*
    | Store (Id id, Typed (Id other,ty)) ->
        match ty with
        | StringType -> emit state $"store i8* {other}, i8** %%{id}, align 8"
        | IntType ->  emit state $"store i32 {other}, i32* %%{id}, align 4"
        ""
        *)
        
    | Store (Id id, Typed(node,ty)) ->
        let rhs = codegen_expr state (Typed(node,ty))
        
        match ty with
        | StringType -> emit state $"store i8* {rhs}, i8** %%{id}, align 8"
        | IntType ->  emit state $"store i32 {rhs}, i32* %%{id}, align 4"
        ""
        
    | Call (id, args) ->
        
        let gen_arg =
            function
            | Typed(x, argt) ->
                let typ = TypeToLLVM argt
                let reg = codegen_expr state (Typed(x, argt))
                $"{typ} {reg}"
        
        let code_args = args |> List.map gen_arg |> String.concat ", "
        match t with
        | VoidType ->
            emit state $"call void @{id} ({code_args})"
            ""
        | _ ->
            let ret_typ = TypeToLLVM t
            let tmp = nextReg state
            emit state $"{tmp} = call {ret_typ} @{id} ({code_args})"
            tmp
            
    | x -> failwith $"CodeGen_expr unsupported %A{x}"

let codegen (program: AST) : string =
    let state =
        {
          Counters = Dictionary()
          Code = StringBuilder()
          StringConstants = StringBuilder()
        }

    
    emit state "define i32 @main() {"
    let result_reg = codegen_expr state program
    emit state $"ret i32 0"
    emit state "}"

    state.StringConstants.ToString() + "\n" + state.Code.ToString()
