trait Type {
}

case class voidType() extends Type

case class intType() extends Type

case class boolType() extends Type

case class stringType() extends Type

case class functionType(returnType:Type) extends Type

case class lambdaType(returnType:Type, argTypes:List[Type]) extends Type

case class arrayType(elementType:Type=null) extends Type

case class unknownType() extends Type