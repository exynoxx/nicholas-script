

trait Tree {
	val ty: Type
}

case class blockNode(children: List[Tree], ty: Type) extends Tree
case class assignNode(id: Tree, body: Tree, definition: Boolean, idx: Int, ty: Type) extends Tree
case class returnNode(body: Tree, ty: Type) extends Tree
case class functionNode(id: String, args: List[Tree], body: Tree, ty: Type) extends Tree
case class argNode(name: String, ty: Type) extends Tree
case class ifNode(condition: Tree, body: Tree, elsebody: Option[Tree], ty: Type) extends Tree
case class whileNode(condition: Tree, body: Tree, ty: Type) extends Tree
case class binopNode(numbers: List[Tree], ops: List[Tree], idx: Int, ty: Type) extends Tree
case class opNode(body: String, ty: Type) extends Tree
case class valueNode(value: String, ty: Type) extends Tree
case class callNode(id: String, args: List[Tree], definition: Boolean, ty: Type) extends Tree
case class nullLeaf(ty: Type = simpleType("null",null)) extends Tree
case class arrayNode(elements: List[Tree], ty: Type) extends Tree
case class lineNode(text:String, ty: Type) extends Tree
case class rangeNode(from: Tree, to:Tree,ty: Type) extends Tree
case class accessNode(name:String,index:Tree,ty: Type) extends Tree
case class propertyNode(body:Tree,funcall:Tree, definition: Boolean, ty: Type) extends Tree
case class forNode(variable:Tree, array:Tree, body: Tree, ty: Type) extends Tree
case class anonNode(args: List[Tree], body: Tree, ty: Type) extends Tree
case class objectNode(id:String, rows: List[Tree], ty: Type) extends Tree
case class objectElementNode(name:String, ty: Type) extends Tree
case class objectInstansNode(name:String,args:List[Tree], ty: Type) extends Tree
case class objectAssociatedFunctionNode(name:String,functions:List[Tree],ty: Type) extends Tree
case class specialArgNode(content: String, ty: Type) extends Tree
case class overrideNode(op:Tree,function:Tree,ty: Type) extends Tree
