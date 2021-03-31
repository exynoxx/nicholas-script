trait Type {
	val ty:Type
}


case class simpleType(stringTy: String, ty: Type) extends Type

case class arrayTypeNode(ty: Type) extends Type

case class functionTypeNode(args: List[Type], ty: Type) extends Type

case class objectTypeNode(args: List[Type], ty: Type) extends Type

case class objectInstansTypeNode(args: List[Type], ty: Type) extends Type
