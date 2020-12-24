

trait Tree {
	val nstype: String
}

case class blockNode(children: List[Tree], nstype: String) extends Tree
case class assignNode(id: String, body: Tree, definition: Boolean, idx: Int, nstype: String) extends Tree
case class returnNode(body: Tree, nstype: String) extends Tree
case class functionNode(id: String, args: List[Tree], body: Tree, nstype: String) extends Tree
case class argNode(name: String, nstype: String) extends Tree
case class ifNode(condition: Tree, body: Tree, elsebody: Option[Tree], nstype: String) extends Tree
case class whileNode(condition: Tree, body: Tree, nstype: String) extends Tree
case class binopNode(numbers: List[Tree], ops: List[Tree], idx: Int, nstype: String) extends Tree
case class opNode(body: String, nstype: String) extends Tree
case class valueNode(value: String, nstype: String) extends Tree
case class callNode(id: String, args: List[Tree], definition: Boolean, nstype: String) extends Tree
case class nullLeaf(nstype: String = "null") extends Tree
case class arrayNode(elements: List[Tree], nstype:String) extends Tree
case class allocNode(name: String, size: Int, nstype: String = null) extends Tree
case class freeNode(variable: String, nstype: String = null) extends Tree
case class lineNode(text:String, nstype:String) extends Tree
case class rangeNode(from: Tree, to:Tree,nstype:String) extends Tree
case class accessNode(name:String,index:Tree,nstype:String) extends Tree
case class propertyNode(body:Tree,funcall:Tree, definition: Boolean, nstype:String) extends Tree