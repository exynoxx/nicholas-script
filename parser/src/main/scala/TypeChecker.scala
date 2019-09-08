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
				val ss = newNum.exists { case e: Tree => (e.nstype == "string" | e.nstype == "actualstring") }
				val ty: String = if (ss) "string" else newNum.head.nstype
				(binopNode(newNum, ops, idx, ty), symbol)

			case assignNode(id, body, deff, idx, ns) =>
				val (btree, _) = typerecurse(body, AST, symbol)
				val ty = ns match {
					case null => btree.nstype
					case x => x
				}
				val idmap = HashMap(id -> btree.nstype)
				val s = btree match {
					case valueNode(n, "actualstring") =>
						val hashID = "size" + id
						val sizemap = HashMap(hashID -> (n.length - 2).toString)
						symbol ++ sizemap ++ idmap
					case _ => idmap
				}
				(assignNode(id, btree, deff, idx, ty), s)
			case functionNode(_, args, body, ns) =>
				//get id from assign parent
				val id = parent match {
					case assignNode(name, _, _, _, _) => name
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

				//val newSym = symbol + (id -> args.mkString(","))

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
				(callNode(id, newargs, deff, ty), symbol)
			case returnNode(body, ns) =>
				val (newbody, _) = typerecurse(body, AST, symbol)
				(returnNode(newbody, newbody.nstype), symbol)

			case arrayNode(elem, ns) =>
				(arrayNode(elem, "array("+elem.head.nstype+")"), symbol)

			case accessNode(name, index, ns) =>
				val arrayName = symbol.get(name).get
				val newTy = arrayName match {
					case Util.arrayTypePattern(ty) => ty
				}
				(accessNode(name,index,newTy), symbol)
			case x => (x, symbol)
		}
	}

	def iterateBlock(blockBody: List[Tree]): List[Tree] = {
		blockBody match {
			case x :: xs => {
				val ret: List[Tree] = x match {
					case binopNode(numbers, ops, idx, "string") =>
						//assignNode(id, binopNode(l, r, o, "string"), deff, idx, assignNS) => {
						val retList = ListBuffer[Tree]()
						val names = numbers.map {
							case valueNode(vn, "actualstring") =>
								val n = Util.genRandomName()
								val tmp = valueNode(vn, "actualstring")
								retList += assignNode(n, tmp, true, 0, "string")
								valueNode(n, "string")
							case x => x
						}
						retList += binopNode(names, ops, 0, "string")
						retList.toList

					case assignNode(id, body, deff, idx, ns) =>

						//body might append multiple elem infront.
						// last element is actual body
						val list: List[Tree] = iterateBlock(List(body))
						list.reverse match {
							case x :: xs => x match {
								case functionNode(_, _, _, _) => List(x)
								case _ => xs ++ List(assignNode(id, x, deff, idx, ns))
							}
						}

					case functionNode(id, args, body, ns) =>
						val fbody = iterateBlock(List(body))(0)
						val retbody = fbody match {
							case binopNode(l, r, o, ns) =>
								val idName = Util.genRandomName()
								val tmpAssign = assignNode(idName, binopNode(l, r, o, ns), true, 0, ns)
								val tmp = returnNode(valueNode(idName, ns), ns)
								blockNode(List(tmpAssign, tmp), ns)
							case valueNode(value, ns) =>
								val tmp = returnNode(valueNode(value, ns), ns)
								blockNode(List(tmp), ns)
							case _ => fbody
						}
						List(functionNode(id, args, retbody, ns))
					case blockNode(children, ns) =>
						val b: Tree = augment(blockNode(children, ns))
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
							case valueNode(nm, "actualstring") =>
								val n = Util.genRandomName()
								val ns = "actualstring"
								val preassign = assignNode(n, valueNode(nm, ns), true, 0, ns)
								val replaceElement = valueNode(n, "string")
								tmpList += preassign
								replaceElement
							case binopNode(l, r, o, ns) =>
								val n = Util.genRandomName()
								val preassign = assignNode(n, binopNode(l, r, o, ns), true, 0, ns)
								val replaceElement = valueNode(n, ns)
								tmpList += preassign
								replaceElement
							case x => x

						}
						tmpList += callNode(id, newargs, deff, ns)
						tmpList.toList
					case returnNode(body, ns) =>
						val shouldExtract = body match {
							case valueNode(_, "actualstring") => true
							case valueNode(name, ns) => false
							case _ => true
						}
						if (shouldExtract) {
							val id = Util.genRandomName()
							val preassign = assignNode(id, body, true, 0, ns)
							val replaceElement = valueNode(id, ns)
							preassign :: List(returnNode(replaceElement, ns))
						} else {
							List(returnNode(body, ns))
						}
					case arrayNode(elem, ns) =>
						val tmpList = ListBuffer[Tree]()
						val newelem = elem.map {
							case valueNode(value, ns) => valueNode(value, ns)
							case binopNode(numbers, ops, idx, ns) => binopNode(numbers, ops, idx, ns)
							case x =>
								val id = Util.genRandomName()
								val preassign = assignNode(id, x, true, 0, x.nstype)
								val replaceElement = valueNode(id, x.nstype)
								tmpList += preassign
								replaceElement
						}
						val newTy = newelem.head.nstype match {
							case "string" => "array(string)"
							case "actualstring" => "array(string)"
							case "int" => "array(int)"
						}
						tmpList += arrayNode(newelem, newTy)
						tmpList.toList
					case rangeNode(from, to, ns) =>
						val tmpList = ListBuffer[Tree]()
						val extract = (x: Tree) => {
							x match {
								case valueNode(v, vns) =>
									valueNode(v, vns)
								case y =>
									val id = Util.genRandomName()
									val assign = assignNode(id, y, true, 0, "int")
									val valn = valueNode(id, "Int")
									tmpList += assign
									valn
							}
						}
						val newfrom = extract(from)
						val newto = extract(to)
						tmpList += rangeNode(newfrom, newto, ns)
						tmpList.toList

					case t => List(t)
				}

				ret ++ iterateBlock(xs)
			}
			case _ => List()
		}
	}

	//TODO string input in func annotate correct size at runtime
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
