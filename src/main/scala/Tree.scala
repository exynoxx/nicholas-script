

trait Tree {
}

case class nullLeaf() extends Tree

case class binopNode(op: String, left: Tree, right: Tree) extends Tree

case class unopNode(op: String, exp: Tree) extends Tree

case class integerNode(value: Int) extends Tree

case class boolNode(value: Boolean) extends Tree

case class stringNode(value: String) extends Tree

case class wordNode(value: String) extends Tree

case class assignNode(id: Tree, body: Tree) extends Tree

case class reassignNode(id: Tree, body: Tree) extends Tree

case class arrayNode(elements: List[Tree]) extends Tree

case class blockNode(children: List[Tree]) extends Tree

case class metaNode(name:String, extractName:String) extends Tree
case class functionNode(args: List[Tree], body: Tree, metaData: metaNode) extends Tree

case class returnNode(exp: Tree) extends Tree

case class callNode(f: Tree, args: List[Tree]) extends Tree

case class accessNode(array: Tree, index: Tree) extends Tree

case class loopNode(variable: Tree, from: Tree, to: Tree, step: Tree, body: Tree)

case class castNode(from: Tree, to: Tree, toTyp: Type) extends Tree

case class arrayMultNode(element: Tree, amount: Tree) extends Tree

case class libraryCallNode(fname: String, expr: List[Tree]) extends Tree

case class sequenceNode(list: List[Tree]) extends Tree

case class ifNode(condition: Tree, body: Tree, elseBody: Option[Tree], returnName:String=null) extends Tree

case class mapNode(f:Tree, array:Tree) extends Tree

case class comprehensionNode(body:Tree, variable:Tree, array:Tree, filter:Option[Tree]) extends Tree

/*
case class returnNode(body: Tree, nstype: String) extends Tree
case class functionNode(id: String, args: List[Tree], body: Tree, nstype: String) extends Tree
case class argNode(name: String, nstype: String) extends Tree
case class ifNode(condition: Tree, body: Tree, elsebody: Option[Tree], nstype: String) extends Tree
case class whileNode(condition: Tree, body: Tree, nstype: String) extends Tree
case class opNode(body: String, nstype: String) extends Tree
case class valueNode(value: String, nstype: String) extends Tree
case class callNode(id: String, args: List[Tree], definition: Boolean, nstype: String) extends Tree
case class lineNode(text:String, nstype:String) extends Tree
case class rangeNode(from: Tree, to:Tree,nstype:String) extends Tree
case class accessNode(name:String,index:Tree,nstype:String) extends Tree
case class propertyNode(body:Tree,funcall:Tree, definition: Boolean, nstype:String) extends Tree
case class forNode(variable:Tree, array:Tree, body: Tree, nstype: String) extends Tree
case class anonNode(args: List[Tree], body: Tree, nstype: String) extends Tree
case class objectNode(id:String, rows: List[Tree], nstype:String) extends Tree
case class objectElementNode(name:String, nstype:String) extends Tree
case class objectInstansNode(name:String,args:List[Tree], nstype:String) extends Tree
case class objectAssociatedFunctionNode(name:String,functions:List[Tree],nstype:String) extends Tree
case class specialArgNode(content: String, nstype: String) extends Tree
case class overrideNode(op:Tree,function:Tree,nstype:String) extends Tree
*/

