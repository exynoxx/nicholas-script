import scala.collection.mutable.ArrayBuffer

trait Tree {
	val nstype: String
}

case class blockNode(children:List[Tree], nstype: String) extends Tree

case class assignNode(id: String, body: Tree, nstype: String) extends Tree

case class statementNode(body: Tree, nstype:String) extends Tree

case class functionNode(id: String, args: ArrayBuffer[Tree], body: Tree, nstype: String) extends Tree

case class ifNode(condition: Tree, body: Tree, elsebody: Option[Tree], nstype: String) extends Tree

case class whileNode(condition: Tree, body: Tree, nstype: String) extends Tree

case class binopNode(left: Tree, right: Tree, op: Tree, nstype: String) extends Tree

case class opNode(body:String, nstype:String) extends Tree

case class valueNode(value: String, nstype: String) extends Tree

case class callNode(id: String, args: ArrayBuffer[Tree], nstype: String) extends Tree

case class nullLeaf(nstype: String = "null") extends Tree
