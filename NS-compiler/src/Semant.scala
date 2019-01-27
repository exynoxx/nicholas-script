
class Semant {

    def typeAnnotate(tree: Tree, env: Map[String, String]): (Tree, Map[String, String]) = {

        def findReturn(x: Tree): Option[String] = x match {
            case returnNode(_, ns) => Some(ns)
            case ifNode(_, b, e, _) => findReturn(b).orElse(findReturn(e))
            case whileNode(cond, body, ns) => {

                //is body a block?
                body match {
                    case blockNode(children, _) => {

                        //find return statement inside the block
                        var retTy: Option[String] = None
                        children.map((x) => findReturn(x) match {
                            case Some(somety) => retTy = Some(somety)
                            case _ => None
                        })
                        retTy
                    }
                    //case _ => findReturn(_)
                }
            }
            //a block node will have its own return
            case _ => None
        }

        tree match {
            case programNode(children, ns) => {
                //val c = children.map(typeAnnotate(_,env))

                var mergeenv = env
                val newchildren = children.map((x) => {
                    val (y, e) = typeAnnotate(x, mergeenv)
                    mergeenv = mergeenv ++ e
                    y
                })

                return (programNode(newchildren, ns), env)
            }

            //TODO: get type from body|els with actual type
            //body OR elsbody can have different type. fix
            case ifNode(cond, body, els, ns) => {
                val (c, env1) = typeAnnotate(cond, env)
                val (b, env2) = typeAnnotate(body, env)
                val (e, env3) = typeAnnotate(els, env)
                return (ifNode(c, b, e, b.nstype), env)
            }

            case assignNode(id, body, ns) => {
                val (body1, env1) = typeAnnotate(body, env)
                val body2 = body1 match {
                    case callNode(i, a, ns, _) => callNode(i, a, ns, true)
                    case _ => body1
                }
                val ty = body2.nstype
                val newenv = env + (id -> ty)
                return (assignNode(id, body2, ty), newenv)
            }

            //TODO:report error
            case incNode(id, body, sign, ns) => {
                val value = valueNode(id, false, true, "")
                val ty = env.get(id) match {
                    case Some(t) => t
                    case None => "int"
                }
                val newbody = binopNode(value, sign, typeAnnotate(body, env)._1, ty)
                return (assignNode(id, newbody, ty), env)
            }

            case binopNode(left, sign, right, ns) => {
                val (newleft, env1) = typeAnnotate(left, env)
                val (newright, env2) = typeAnnotate(right, env)
                return (binopNode(newleft, sign, newright, newleft.nstype), env)
            }

            case valueNode(value, string, variable, ns) => {
                var ty = ns
                if (variable) {
                    ty = env.get(value).get
                }
                return (valueNode(value, string, variable, ty), env)
            }

            case whileNode(cond, body, ns) => {
                val (b, env1) = typeAnnotate(body, env)
                val (c, env2) = typeAnnotate(cond, env)
                return (whileNode(c, b, b.nstype), env)
            }

            case callNode(id, args, ns, c) => {
                val newargs = args.map(typeAnnotate(_, env))
                return (callNode(id, args, ns, c), env)
            }

            case blockNode(children, ns) => {
                var mergeenv = env
                val newchildren = children.map((x) => {
                    val (y, e) = typeAnnotate(x, mergeenv)
                    mergeenv = mergeenv ++ e
                    y
                })


                var ty: String = ns


                newchildren.map((x) => findReturn(x) match {
                    case Some(somety) => ty = somety
                    case _ => None
                })
                return (blockNode(newchildren, ty), env)
            }

            case returnNode(body, ns) => {
                val (b, env1) = typeAnnotate(body, env)
                return (returnNode(b, b.nstype), env)
            }

            case functionNode(args, body, ns) => {
                val (newbody, env1) = typeAnnotate(body, env)
                return (functionNode(args, newbody, newbody.nstype), env)
            }

            case x => return (x, env)
        }
    }

}
