import ANTLR.{Node, Type}

import scala.collection.mutable.ArrayBuffer

class JavaToScalaBind {

    def arrayListToArrayBuffer (list: java.util.ArrayList[Node]):ArrayBuffer[Tree] = {
        var l = new ArrayBuffer[Tree](list.size)
        for (i <- 0 to list.size-1) {
            l += convert(list.get(i))
        }

        return l
    }

    def convert (rootnode:Node):Tree = {
        if (rootnode.`type` == Type.PROGRAM) {
            return programNode(arrayListToArrayBuffer(rootnode.children),"")
        }

        if (rootnode.`type` == Type.IF) {
            return ifNode(convert(rootnode.cond),convert(rootnode.body),convert(rootnode.elsebody),"")
        }
        if (rootnode.`type` == Type.ASSIGN) {
            return assignNode(rootnode.ID,convert(rootnode.body),rootnode.nstype)
        }
        if (rootnode.`type` == Type.INCOP) {
            return incNode(rootnode.ID,convert(rootnode.body), rootnode.sign,"")
        }
        if (rootnode.`type` == Type.WHILE) {
            return whileNode(convert(rootnode.cond),convert(rootnode.body),"")
        }
        if (rootnode.`type` == Type.RETURN) {
            return returnNode(convert(rootnode.body),"")
        }
        if (rootnode.`type` == Type.CALL) {
            return callNode(rootnode.ID, arrayListToArrayBuffer(rootnode.args),"",false)
        }
        if (rootnode.`type` == Type.BINOP) {
            return binopNode(convert(rootnode.value),rootnode.sign,convert(rootnode.body),"")
        }
        if (rootnode.`type` == Type.VALUE) {
            return valueNode(rootnode.text,false,false,"int")
        }
        if (rootnode.`type` == Type.VALUESTRING) {
            return valueNode(rootnode.text,true,false, "string")
        }
        if (rootnode.`type` == Type.VALUEVARIABLE) {
            return valueNode(rootnode.text,false,true,"")
        }
        if (rootnode.`type` == Type.BLOCK) {
            return blockNode(arrayListToArrayBuffer(rootnode.children),rootnode.nstype)
        }
        if (rootnode.`type` == Type.FUNCTION) {
            return functionNode("",arrayListToArrayBuffer(rootnode.args),convert(rootnode.body),rootnode.nstype)
        }
        if (rootnode.`type` == Type.ARG) {
            return argNode(rootnode.ID,rootnode.nstype)
        }
        return nullLeaf("")
    }


}
