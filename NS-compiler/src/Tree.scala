import scala.collection.mutable.ArrayBuffer

trait Tree {
    val nstype:String
}
case class programNode(children: ArrayBuffer[Tree], nstype:String) extends Tree
case class assignNode(id: String, body: Tree, nstype:String) extends Tree
case class incNode(id:String, body: Tree, sign: String, nstype:String) extends Tree
case class ifNode (condition: Tree, body: Tree, elsebody: Tree, nstype:String) extends Tree
case class whileNode (condition: Tree, body: Tree, nstype:String) extends Tree
case class returnNode (body: Tree, nstype:String) extends Tree
case class binopNode (left: Tree, sign: String, right: Tree, nstype:String) extends Tree
case class callNode (id:String, args: ArrayBuffer[Tree], nstype:String,child:Boolean) extends Tree
case class valueNode (value: String, string: Boolean,variable:Boolean, nstype:String) extends Tree
case class blockNode (children: ArrayBuffer[Tree], nstype:String) extends Tree
case class functionNode (args: ArrayBuffer[Tree], body: Tree, nstype:String) extends Tree
case class argNode(id:String, nstype:String) extends Tree
case class nullLeaf(nstype:String) extends Tree