import scala.collection.immutable.HashMap
import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TypeChecker {

    def typecheck(AST: Tree): Tree = {
        val (t, _) = typerecurse(AST, AST, HashMap())
        t
    }

    def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, String]): (Tree, HashMap[String, String]) = {
        AST match {
            case valueNode(value, ns) =>
                if (value.matches("\\d+"))
                    (valueNode(value, "int"), symbol)
                else if (value.matches("\"[\\w\\d]*\""))
                    (valueNode(value, "string"), symbol)
                else
                    (valueNode(value, symbol(value)), symbol)
            case binopNode(l, r, o, ns) =>
                val (leftTree, _) = typerecurse(l, AST, symbol)
                val (rightTree, _) = typerecurse(r, AST, symbol)
                (binopNode(leftTree, rightTree, o, leftTree.nstype), symbol)
            //case opNode(op, _) => codeblock(ret = op)
            case assignNode(id, body, deff, ns) =>
                val (btree, _) = typerecurse(body, AST, symbol)
                val ty = ns match {
                    case null => btree.nstype
                    case x => x
                }
                (assignNode(id, btree, deff, ty), symbol + (id -> btree.nstype))
            case functionNode(_, args, body, ns) =>
                //get id from assign parent
                val id = parent match {
                    case assignNode(name, _, _, _) => name
                }

                //update symbol table with args
                var s = symbol
                args.foreach { case argNode(name: String, ty: String) => s = s + (name -> ty) }

                //recurse body
                val (fbody, _) = typerecurse(body, AST, s)

                //if type defined by syntax, use that
                val ty = ns match {
                    case null => fbody.nstype
                    case x => x
                }

                //ret
                (functionNode(id, args, fbody, ty), symbol)
            //case argNode(name, ns) =>
            case blockNode(children, _) =>
                var s = symbol
                val newkids = children.map { e =>
                    val (t, sym) = typerecurse(e, AST, s)
                    s = s ++ sym
                    t
                }
                var ns = "void"
                newkids.foreach {
                    case returnNode(b, ty) => ns = ty
                    case _ =>
                }
                (blockNode(newkids, ns), symbol)
            case ifNode(c, b, Some(els), ns) =>
                val (ifbody, _) = typerecurse(b, AST, symbol)
                val (elsbody, _) = typerecurse(els, AST, symbol)
                (ifNode(c, ifbody, Some(elsbody), ifbody.nstype), symbol)
            case ifNode(c, b, None, ns) =>
                val (ifbody, _) = typerecurse(b, AST, symbol)
                (ifNode(c, ifbody, None, ifbody.nstype), symbol)
            case whileNode(c, b, ns) =>
                val (nb, _) = typerecurse(b, AST, symbol)
                (whileNode(c, nb, ns), symbol)
            case callNode(id, args, deff, ns) =>
                val newargs = args.map { e =>
                    val (t, _) = typerecurse(e, AST, symbol)
                    t
                }
                val ty = symbol(id)
                (callNode(id, args, deff, ty), symbol)
            case returnNode(body, ns) =>
                val (newbody, _) = typerecurse(body, AST, symbol)
                (returnNode(newbody, newbody.nstype), symbol)
        }
    }

    def iterateBlock(blockBody: List[Tree]): List[Tree] = {
        blockBody match {
            case x :: xs => {
                val ret: List[Tree] = x match {
                    case assignNode(id, binopNode(l, r, o, "string"), deff, assignNS) => {
                        val retList = ListBuffer[Tree]()
                        val lname = l match {
                            case valueNode(Util.stringPattern(c), _) =>
                                val n = Util.genRandomName()
                                retList += assignNode(n, l, true, "string")
                                n
                            case valueNode(n, _) => n

                        }
                        val rname = r match {
                            case valueNode(Util.stringPattern(c), _) =>
                                val n = Util.genRandomName()
                                retList += assignNode(n, r, true, "string")
                                n
                            case valueNode(n, _) => n

                        }
                        val tmpMID = binopNode(valueNode(lname, "string"), valueNode(rname, "string"), o, "string")
                        val tmpFinal = assignNode(id, tmpMID, deff, assignNS)
                        retList += tmpFinal
                        retList.toList
                    }
                    case assignNode(id, body, deff, ns) =>
                        val list:List[Tree] = iterateBlock(List(body))
                        list.reverse match {
                            case x::xs => xs ++ List(assignNode(id, x, deff, ns))
                        }


                    case functionNode(id, args, body, ns) =>
                        val fbody = iterateBlock(List(body))(0)
                        val retbody = fbody match {
                            case binopNode(l, r, o, ns) =>
                                val tmp = returnNode(binopNode(l, r, o, ns), ns)
                                blockNode(List(tmp), ns)
                            case valueNode(value,ns) =>
                                val tmp = returnNode(valueNode(value,ns), ns)
                                blockNode(List(tmp), ns)
                            case _ => fbody
                        }
                        List(functionNode(id, args, retbody, ns))
                    case blockNode(children, ns) =>
                        val b:Tree = augment(blockNode(children, ns))
                        List(b)
                    case ifNode(c, b, els, ns) =>
                        val ifbody = iterateBlock(List(b))(0)
                        val elsbody = els match {
                            case Some(els) => Some(iterateBlock(List(els))(0))
                            case None => None
                        }
                        List(ifNode(c, ifbody, elsbody, b.nstype))
                    case whileNode(c, b, ns) =>
                        List(whileNode(c, iterateBlock(List(b))(0), ns))
                    case callNode(id, args, deff, ns) =>
                        val tmpList = ListBuffer[Tree]()
                        val newargs = args.map {
                            case valueNode(Util.stringPattern(c), vns) =>
                                val n = Util.genRandomName()
                                val preassign = assignNode(n, valueNode(c, "string"), true, "string")
                                val replaceElement = valueNode(n, "string")
                                tmpList += preassign
                                replaceElement
                            case x => x
                        }
                        tmpList += callNode(id, newargs, deff, ns)
                        tmpList.toList
                    case returnNode(body,ns) =>
                        val shouldExtract = body match {
                            case valueNode(Util.stringPattern(c),ns) => true
                            case valueNode(name, ns) => false
                            case _ => true
                        }
                        if (shouldExtract) {
                            val id = Util.genRandomName()
                            val preassign = assignNode(id, body, true, ns)
                            val replaceElement = valueNode(id, ns)
                            preassign::List(returnNode(replaceElement,ns))
                        } else {
                            List(returnNode(body,ns))
                        }

                    case t => List(t)
                }

                ret ++ iterateBlock(xs)
            }
            case _  => List()
        }
    }

    def augment(AST: Tree): Tree = {
        AST match {
            case blockNode(children, ns) => {
                val newchildren = iterateBlock(children)
                blockNode(newchildren, ns)
            }
            case _ => println("error")
                nullLeaf()
        }
    }

}
