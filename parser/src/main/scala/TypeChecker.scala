import scala.collection.immutable.HashMap
import scala.collection.mutable.ListBuffer

class TypeChecker {

	def typecheck(AST: Tree): Tree = {
		val (t, _) = typerecurse(AST, AST, HashMap())
		t
	}

	def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, String]): (Tree, HashMap[String, String]) = {
		AST match {
			case valueNode(value, ns) =>
				val newns = ns match {
					case null =>
						symbol(value) match {
							case "actualstring" => "string"
							case x => x
						}
					case _ => ns
				}
				(valueNode(value, newns), symbol)

			case binopNode(numbers, ops, idx, ns) =>
				val newNum: List[Tree] = numbers.map(e => typerecurse(e, AST, symbol)._1)
				val stringTypeExist = newNum.exists { case e: Tree => e.nstype == "string" | e.nstype == "actualstring" }
				val boolTypeExist = ops.exists { case opNode(o, t) => t == "bool" }
				val ty: String =
					if (stringTypeExist) {
						"string"
					} else if (boolTypeExist)
						"bool"
					else {
						"int"
					}

				(binopNode(newNum, ops, idx, ty), symbol)

			case assignNode(id, body, deff, idx, ns) =>
				//val (idtree,_) = typerecurse(id, AST, symbol)
				val (btree, _) = typerecurse(body, AST, symbol)
				val ty = ns match {
					case null => btree.nstype
					case x => x
				}

				val textid = id match {
					case valueNode(rid, _) => rid
					case x => x.toString
				}

				val idmap = HashMap(textid -> btree.nstype)
				val s = btree match {
					case valueNode(n, "actualstring") =>
						val hashID = "size" + textid
						val sizemap = HashMap(hashID -> (n.length - 2).toString)
						symbol ++ sizemap ++ idmap
					case _ => idmap
				}
				(assignNode(id, btree, deff, idx, ty), s)
			case functionNode(_, args, body, ns) =>
				//get id from assign parent
				val id = parent match {
					case assignNode(valueNode(name, _), _, _, _, _) => name
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
				val (nc, _) = typerecurse(c, AST, symbol)
				val (nb, _) = typerecurse(b, AST, symbol)
				(whileNode(nc, nb, ns), symbol)
			case callNode(id, args, deff, ns) =>
				val newargs = args.map { e =>
					val (t, _) = typerecurse(e, AST, symbol)
					t
				}
				val ty = symbol(id)
				(callNode(id, newargs, deff, ty), symbol)
			case returnNode(body, ns) =>
				val (newbody, _) = typerecurse(body, AST, symbol)
				(returnNode(newbody, newbody.nstype), symbol)

			case arrayNode(elem, ns) =>
				(arrayNode(elem, "array(" + elem.head.nstype + ")"), symbol)

			case accessNode(name, index, ns) =>
				val arrayName = symbol.get(name).get
				val newTy = arrayName match {
					case Util.arrayTypePattern(ty) => ty
				}
				val (idx, _) = typerecurse(index, AST, symbol)
				(accessNode(name, idx, newTy), symbol)

			case lineNode(t, ns) => (lineNode(t, "void"), symbol)
			case x => (x, symbol)
		}
	}
}
