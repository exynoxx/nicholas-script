
class Semant {

    def typeAnnotate(tree: Tree, env:Map[String,String]): Tree = {
        tree match {
            case programNode(children, ns) => {
                val c = children.map(typeAnnotate(_,env))
                return programNode(c, ns)
            }

            case ifNode(cond, body, els, ns) => {
                return ifNode(typeAnnotate(cond,env), typeAnnotate(body,env), typeAnnotate(els,env), "")
            }

            case assignNode(id, body, ns) => {
                val newbody = typeAnnotate(body,env)
                val ty = newbody.nstype
                return assignNode(id, newbody, ty)
            }

            //TODO:report error
            case incNode(id, body, sign, ns) => {
                val value = valueNode(id, false, true, "")
                val ty = env.get(id) match {
                    case Some(t) => t
                    case None => "int"
                }
                val newbody = binopNode(value, sign, typeAnnotate(body,env), ty)
                return assignNode(id, newbody, ty)
            }

            case binopNode(left, sign, right, ns) => {
                val newleft = typeAnnotate(left,env)
                val newright = typeAnnotate(right,env)
                return binopNode(newleft, sign, newright, newleft.nstype)
            }

            case whileNode(cond, body, ns) => {
                return whileNode(typeAnnotate(cond,env), typeAnnotate(body,env), "")
            }

            case callNode(id, args, ns) => {
                val newargs = args.map(typeAnnotate(_,env))
                return callNode(id,args,ns)
            }

            case blockNode(children, ns) => {
                val newchildren = children.map(typeAnnotate(_,env))
                var ty: String = null
                newchildren.map((x) => x match {case returnNode(_,ns) => ty = ns})
                return blockNode(newchildren, ty)
            }

            case functionNode(args, body, ns) => {
                val newbody = typeAnnotate(body,env)
                return functionNode(args, newbody,newbody.nstype)
            }
        }
    }

}
