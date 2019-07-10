import scala.collection.immutable.HashMap
import scala.collection.mutable

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
				else if (value.matches("\"[\\w\\d]+\""))
					(valueNode(value, "string"), symbol)
				else
                    (valueNode(value, symbol(value)), symbol)
			case binopNode(l, r, o, ns) =>
				(binopNode(l, r, o, "int"), symbol)
			//case opNode(op, _) => codeblock(ret = op)
			case assignNode(id, body,deff, ns) =>
				val (btree, _) = typerecurse(body, AST, symbol)
                val ty = ns match {
                    case null => btree.nstype
                    case x => x
                }
				(assignNode(id, btree,deff, ty), symbol + (id -> btree.nstype))
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
				newkids.foreach { case returnNode(b, ty) => ns = ty
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
            case callNode(id,args,deff,ns) =>
                val newargs = args.map{e =>
                    val (t,_) = typerecurse(e,AST,symbol)
                    t
                }
                val ty = symbol(id)
                (callNode(id,args,deff,ty),symbol)
            case returnNode(body,ns) =>
                val (newbody,_) = typerecurse(body,AST,symbol)
                (returnNode(newbody,newbody.nstype),symbol)
		}
	}

	def augment(AST: Tree): Tree = {
		AST match {
			case assignNode(id, body,deff, ns) => assignNode(id, augment(body),deff, ns)

			case functionNode(id, args, body, ns) =>
				val fbody = augment(body)
				val retbody = fbody match {
					case binopNode(l, r, o, ns) =>
						val tmp = returnNode(binopNode(l, r, o, ns), ns)
						blockNode(List(tmp), ns)
					case _ => fbody
				}
				functionNode(id, args, retbody, ns)
			case blockNode(children, ns) =>
				val newkids = children.map(e => augment(e))
				blockNode(newkids, "")
			case ifNode(c, b, els, ns) =>
				val ifbody = augment(b)
				val elsbody = els match {
					case Some(els) => Some(augment(els))
					case None => None
				}
				ifNode(c, b, elsbody, b.nstype)
			case whileNode(c, b, ns) =>
				whileNode(c, augment(b), ns)
			case t => t
		}
	}

}
