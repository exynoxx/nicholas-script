module NS2.Type

type Type =
    | VoidType
    | IntType
    | StringType
    | BoolType
    | ArrayType
    | FunctionType of Type
    | AnyType