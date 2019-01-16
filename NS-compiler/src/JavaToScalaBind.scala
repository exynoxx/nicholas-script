import scala.collection.convert.ToScalaImplicits
import scala.collection.mutable.ArrayBuffer

class JavaToScalaBind {
    trait Tree
    case class programNode(children: ArrayBuffer[Tree]) extends Tree
    case class assignNode(id: String, body: Tree) extends Tree
    case class incNode(id:String, body: Tree, sign: String) extends Tree
    case class ifNode (condition: Tree, body: Tree, elsebody: Tree) extends Tree
    case class whileNode (condition: Tree, body: Tree) extends Tree
    case class returnNode (body: Tree) extends Tree
    case class binopNode (left: valueNode, sign: String, right: Tree) extends Tree
    case class valueNode (body: String) extends Tree
    case class callNode (id:String, args: ArrayBuffer[Tree]) extends Tree

    def convert (rootnode:Node):Tree = {
        if (rootnode.`type` == Type.PROGRAM) {
            var l = new ArrayBuffer[Tree](rootnode.children.size)
            for (i <- 0 to rootnode.children.size) {
                l += convert(rootnode.children.get(i))
            }
            return programNode(l)
        }
        if (rootnode.`type` == Type.IF) {
            return ifNode(convert(rootnode.cond),convert(rootnode.body),convert(rootnode.elsebody))
        }
        if (rootnode.`type` == Type.ASSIGN) {
            return assignNode(rootnode.ID,convert(rootnode.body))
        }
        if (rootnode.`type` == Type.INCOP) {
            return incNode(rootnode.ID,convert(rootnode.body), rootnode.sign)
        }
        if (rootnode.`type` == Type.WHILE) {
            return whileNode(convert(rootnode.cond),convert(rootnode.body))
        }
        if (rootnode.`type` == Type.RETURN) {
            return returnNode(convert(rootnode.body))
        }
        if (rootnode.`type` == Type.CALL) {
            var l = new ArrayBuffer[Tree](rootnode.args.size)
            for (i <- 0 to rootnode.args.size) {
                l += convert(rootnode.args.get(i))
            }
            return callNode(rootnode.ID, l)
        }
    }


}
