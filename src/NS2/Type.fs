module NS2.Type

type Type =
    | VoidType
    | IntType
    | StringType
    | BoolType
    | ArrayType of Type
    | FunctionType of Type
    | AnyType