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
    case class callNode (id:String, args: ArrayBuffer[Tree]) extends Tree
    case class valueNode (value: String, string: Boolean,variable:Boolean) extends Tree
    case class blockNode (children: ArrayBuffer[Tree]) extends Tree
    case class functionNode (args: ArrayBuffer[Tree], body: Tree) extends Tree
    case class argNode(id:String) extends Tree
    case class nullLeaf() extends Tree

    def arrayListToArrayBuffer (list: java.util.ArrayList[Node]):ArrayBuffer[Tree] = {
        var l = new ArrayBuffer[Tree](list.size)
        for (i <- 0 to list.size-1) {
            l += convert(list.get(i))
        }

        return l
    }

    def convert (rootnode:Node):Tree = {
        if (rootnode.`type` == Type.PROGRAM) {
            return programNode(arrayListToArrayBuffer(rootnode.children))
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
            return callNode(rootnode.ID, arrayListToArrayBuffer(rootnode.args))
        }
        if (rootnode.`type` == Type.VALUE) {
            return valueNode(rootnode.text,false,false)
        }
        if (rootnode.`type` == Type.VALUESTRING) {
            return valueNode(rootnode.text,true,false)
        }
        if (rootnode.`type` == Type.VALUEVARIABLE) {
            return valueNode(rootnode.text,false,true)
        }
        if (rootnode.`type` == Type.BLOCK) {
            return blockNode(arrayListToArrayBuffer(rootnode.children))
        }
        if (rootnode.`type` == Type.FUNCTION) {
            return functionNode(arrayListToArrayBuffer(rootnode.args),convert(rootnode.body))
        }
        if (rootnode.`type` == Type.ARG) {
            return argNode(rootnode.ID)
        }
        return nullLeaf()
    }


}
