trait Type {
}

case class voidType() extends Type

case class intType() extends Type

case class boolType() extends Type

case class stringType() extends Type

case class blockType() extends Type

case class functionType(name:String) extends Type

case class arrayType(elementType:Type=null) extends Type

case class unknownType() extends Type

case class compositeType(operator:String, left:Type,right:Type) extends Type

case class transparentType(name:String) extends Type

