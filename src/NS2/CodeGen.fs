module NS2.CodeGen

open System
open System.Diagnostics.Metrics
open System.Text
open NS2.Ast
open System.Collections.Generic
open NS2.Type

type CodegenState (code: StringBuilder, stringConstants: StringBuilder, counters: Dictionary<string, int>) =
    
    let mutable Lazy = None
    member this.FlushLazy () =
        match Lazy with
        | Some (var,rhs) ->
            Lazy <- None
            code.AppendLine $"{var} = {rhs}" |> ignore
        | None -> ()
    
    member this.EmitAssign(var:string, rhs:string) =
        this.FlushLazy ()
        Lazy <- Some (var,rhs)
        
    member this.FlushLazyNewVar (var:string) =
        match Lazy with
        | None -> failwith "emit lazy none"
        | Some (_,rhs) ->
            Lazy <- None
            code.AppendLine $"{var} = {rhs}" |> ignore
            let r = counters.GetValueOrDefault("reg",0)-1
            counters["reg"] <- r
        
    member this.Emit(s:string) =
        this.FlushLazy ()
        code.AppendLine s |> ignore
    member this.EmitConstant(s:string) = stringConstants.AppendLine s |> ignore
    member this.NextLabel (label:string) =
        let r = counters.GetValueOrDefault(label,0)+1
        counters[label] <- r
        $"{label}{r}"
    member this.NextReg ()=
        let r = counters.GetValueOrDefault("reg",0)+1
        counters["reg"] <- r
        $"%%{r}"
        
    member this.Final() = stringConstants.ToString() + "\n" + code.ToString()
    
let typeof =
    function
    | Typed(_,t) -> t
    | _ -> failwith "typeof node not Typed"
    
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
        let const_name = state.NextLabel "@str"
        let line = $"{const_name} = private unnamed_addr constant [{s.Length+1} x i8] c\"{s}\00\", align 1"
        
        state.EmitConstant line
        let tmp = state.NextReg()
        state.EmitAssign(tmp, $"getelementptr [{s.Length+1} x i8], [{s.Length+1} x i8]* {const_name}, i32 0, i32 0")
        tmp

    | Id name -> $"%%{name}"
    | PtrId name ->
        let typ = TypeToLLVM t
        let tmp = state.NextReg()

        state.EmitAssign(tmp, $"load {typ}, {typ}* %%{name}, align 4")
        tmp
       
    | Assign (Id name, Typed(Int i,_)) ->
        state.Emit $"%%{name} = add i32 {i}, 0"   
        ""
        
    | Assign (Id name, body) ->
        let _ = codegen_expr state body
        state.FlushLazyNewVar $"%%{name}"
        ""
        
    | Assign (PtrId name, body) ->
        let rewrite = Store (Id name, body)
        codegen_expr state rewrite
        
    | Binop (left, op, right) ->
        let typ = left |> typeof |> TypeToLLVM
        let l = codegen_expr state left
        let r = codegen_expr state right
        let result = state.NextReg()
        let op_instr = codegen_op op
        state.EmitAssign(result, $"{op_instr} {typ} {l}, {r}")
        result

    | IfPhi (cond, thenBranch, Some elseBranch, phis) ->
        let cond_reg = codegen_expr state cond

        let thenLabel = state.NextLabel "then"
        let elseLabel = state.NextLabel "else"
        let endLabel = state.NextLabel "endif"
        
        state.Emit $"br i1 {cond_reg}, label %%{thenLabel}, label %%{elseLabel}"
        state.Emit $"{thenLabel}:"
        let then_val = codegen_expr state thenBranch
        state.Emit $"br label %%{endLabel}"

        state.Emit $"{elseLabel}:"
        let else_val = codegen_expr state elseBranch
        state.Emit $"br label %%{endLabel}"

        state.Emit $"{endLabel}:"
        for p in phis do
            match p with
            | Typed (Phi(var, thenvar, elsevar),t) -> 
                let typ = TypeToLLVM t
                state.Emit $"%%{var} = phi {typ} [%%{thenvar}, %%{thenLabel}], [%%{elsevar}, %%{elseLabel}]"
            | x -> codegen_expr state x |> ignore
            
        ""
    
    | WhilePhi(c, b, pre_assign) ->
        let entryLabel = state.NextLabel "entry"
        let condLabel = state.NextLabel "cond"
        let loopLabel = state.NextLabel "loop"
        let exitLabel = state.NextLabel "exit"

        state.Emit $"br label %%{entryLabel}"
        state.Emit $"{entryLabel}:"
        
        for x in pre_assign do
            codegen_expr state x
        
        state.Emit $"br label %%{condLabel}"

        state.Emit $"{condLabel}:"
        let cond_reg = codegen_expr state c
        state.Emit $"br i1 {cond_reg}, label %%{loopLabel}, label %%{exitLabel}"

        state.Emit $"{loopLabel}:"
        codegen_expr state b
        state.Emit $"br label %%{condLabel}"
        state.Emit $"{exitLabel}:"
        ""
        
    | CreatePtr (Id id, ty) -> 
        match ty with
        | StringType -> state.Emit $"%%{id} = alloca i8*, align 8"
        | IntType ->  state.Emit $"%%{id} = alloca i32, align 4"
        ""

        
    | Store (Id id, Typed(node,ty)) ->
        let rhs = codegen_expr state (Typed(node,ty))
        
        match ty with
        | StringType -> state.Emit $"store i8* {rhs}, i8** %%{id}, align 8"
        | IntType ->  state.Emit $"store i32 {rhs}, i32* %%{id}, align 4"
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
            state.Emit $"call void @{id} ({code_args})"
            ""
        | _ ->
            let ret_typ = TypeToLLVM t
            let tmp = state.NextReg()
            state.Emit $"{tmp} = call {ret_typ} @{id} ({code_args})"
            tmp
            
    | x -> failwith $"CodeGen_expr unsupported %A{x}"

let codegen (program: AST) : string =
    
    let state = CodegenState(StringBuilder(),StringBuilder(),Dictionary())
    
    state.Emit "define i32 @main() {"
    let result_reg = codegen_expr state program
    state.Emit $"ret i32 0"
    state.Emit "}"

    state.Final()
