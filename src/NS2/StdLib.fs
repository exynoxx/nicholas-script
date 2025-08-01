module NS2.StdLib

open System
open System.Collections.Generic
open NS2.Ast
open NS2.Type

let variants = Dictionary<string, Type list list>()
let variant_to_native = Dictionary<string*Type list,string>()
let ret_type = Dictionary<string,Type>()

let register (name:string, input:Type list, native:string, ret: Type) =
    variants[name] <- variants.GetValueOrDefault(name, []) @ [input]
    variant_to_native[(name,input)] <- native
    ret_type[name] <- ret
let lookup_std_function (name:string)=
    match ret_type.ContainsKey name with
    | true -> Some ret_type[name]
    | false -> None
let translate_std_function (name:string) (args: Type list) = variant_to_native[(name, args)]

register("print", [StringType], "_ns_print_string", VoidType)
register("print", [IntType], "_ns_print_int", VoidType)
register("io.stdin", [] , "_ns_print_int", VoidType)

let LLVM_declares =
    """
        declare void @_ns_print_int(i32)
        declare void @_ns_print_string(i8*)
        declare i8* @_ns_int_to_string(i32)
        declare i8* @_ns_string_concat(i8*, i32)
        declare i32 @_ns_pow_int(i32, i32)
        declare i8* @_ns_readline()
        
        declare noalias i8* @malloc(i64)
        declare void @free(i8*)
        
        %_ns_array = type { i32, i32, i32* }
        @_ns_struct_size = constant i64 ptrtoint (%_ns_array* getelementptr (%_ns_array, %_ns_array* null, i32 1) to i64)

    """.Replace("  ", "")
    
let llvm_std_functions =
    """
        
        define %_ns_array* @_ns_create_array(i32 %len) {
        entry:
            %_ns_struct_size = load i64, i64* @_ns_struct_size
        
            ; array size in bytes
            %arr_size = sext i32 %len to i64
            %elem_size = mul i64 %arr_size, 4
            %total_size = add i64 %_ns_struct_size, %elem_size

            ; allocate memory
            %raw_mem = call noalias i8* @malloc(i64 %total_size)
            %struct_ptr = bitcast i8* %raw_mem to %_ns_array*

            ; ref count = 1
            %ref_ptr = getelementptr inbounds %_ns_array, %_ns_array* %struct_ptr, i32 0, i32 0
            store i32 1, i32* %ref_ptr

            ; len
            %len_ptr = getelementptr inbounds %_ns_array, %_ns_array* %struct_ptr, i32 0, i32 1
            store i32 %len, i32* %len_ptr

            ; compute array start after struct
            %array_start = getelementptr i8, i8* %raw_mem, i64 %_ns_struct_size
            %arr_ptr = bitcast i8* %array_start to i32*

            ; store pointer
            %data_ptr = getelementptr inbounds %_ns_array, %_ns_array* %struct_ptr, i32 0, i32 2
            store i32* %arr_ptr, i32** %data_ptr

            ret %_ns_array* %struct_ptr
        }
        
                
        define void @_ns_ref_inc(%_ns_array* %arr) {
        entry:
            %ref_ptr = getelementptr %_ns_array, %_ns_array* %arr, i32 0, i32 0
            %old = load i32, i32* %ref_ptr
            %new = add i32 %old, 1
            store i32 %new, i32* %ref_ptr
            ret void
        }

        define void @_ns_ref_dec(%_ns_array* %arr) {
        entry:
            %ref_ptr = getelementptr %_ns_array, %_ns_array* %arr, i32 0, i32 0
            %old = load i32, i32* %ref_ptr
            %new = sub i32 %old, 1
            store i32 %new, i32* %ref_ptr
            %is_zero = icmp eq i32 %new, 0
            br i1 %is_zero, label %free, label %done

        free:
            %raw = bitcast %_ns_array* %arr to i8*
            call void @free(i8* %raw)
            br label %done

        done:
            ret void
            
        }
        
    """.Replace("  ", "")

(*
let eval_std_function (name:string, args: AST list) =
    match name with
    | "std.size" ->
        match args with
        | [String x] -> Some (String (x.Length.ToString()))
        | [Array x] -> Some (String (x.Length.ToString()))
        | _ -> failwith "std.size Argument not string or array"
    | "str.rev" ->
        let input = match args with | [String x] -> x | _ -> failwith "Argument not string"
        Some (String (reverse input))
    | "str.trim" ->
        let input = match args with | [String x] -> x | _ -> failwith "Argument not string"
        Some (String (input.Trim()))
    | "print" ->
        match args.Head with
        | String s -> printfn $"{s}"
        | Int s -> printfn $"{s}"
        | Array a -> printfn $"%A{a}"
        Some Nop
    | "io.stdin.line" -> Some (String (stdin.ReadLine()))
    | "io.stdin.all" -> Some (String (stdin.ReadToEnd()))
    | _ -> None
    *)
    
