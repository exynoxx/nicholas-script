trait Type {
}

case class voidType() extends Type

case class intType() extends Type

case class boolType() extends Type

case class stringType() extends Type

case class blockType() extends Type

case class functionType(args: List[Type], output: Type) extends Type

case class arrayType(typ:Type) extends Type
