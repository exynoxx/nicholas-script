
class Semant {

    def typeAnnotate(tree: Tree): Tree = {
        tree match {
            case programNode(children, ns) => {
                val c = children.map(typeAnnotate(_))
                return programNode(c, ns)
            }

            case ifNode(cond, body, els, ns) => {
                return ifNode(typeAnnotate(cond), typeAnnotate(body), typeAnnotate(els), "")
            }

            case assignNode(id, body, ns) => {
                val newbody = typeAnnotate(body)
                val ty = newbody.nstype
                return assignNode(id, newbody, ty)
            }

            //TODO:type
            case incNode(id, body, sign, ns) => {
                val value = valueNode(id, false, true, "")
                val newbody = binopNode(value, sign, typeAnnotate(body), "")
                return assignNode(id, newbody, "")
            }

            case binopNode(left, sign, right, ns) => {
                val newleft = typeAnnotate(left)
                val newright = typeAnnotate(right)
                return binopNode(newleft, sign, newright, newleft.nstype)
            }

            case whileNode(cond, body, ns) => {
                return whileNode(typeAnnotate(cond), typeAnnotate(body), "")
            }

            case callNode(id, args, ns) => {

            }

            case blockNode(children, ns) => {
                val newchildren = children.map(typeAnnotate(_))

                var blockType: String = null
                newchildren.map((x) => x match {
                        case returnNode(_,ns) => blockType = ns
                    })

                /*val search = (x: Tree) => x match {
                    case returnNode(_, _) => true
                    case _ => false
                }
                val ret = newchildren.find(search).get
                val returnNode(_, ns) = ret
                */

                return blockNode(newchildren, blockType)
            }

            case functionNode(args, body, ns) => {
                val newbody = typeAnnotate(body)
                return functionNode(args, newbody,newbody.nstype)
            }
        }
    }

}
