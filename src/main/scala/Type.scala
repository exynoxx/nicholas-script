trait Type {
	val ty: Type
}

case class simpleType(stringTy: String, ty: Type) extends Type

case class intType(ty: Type) extends Type

case class boolType(ty: Type) extends Type

case class stringType(ty: Type) extends Type

case class arrayType(ty: Type) extends Type

case class functionType(args: List[Type], ty: Type) extends Type

case class objectType(args: List[Type], ty: Type) extends Type

case class objectInstansType(args: List[Type], ty: Type) extends Type

