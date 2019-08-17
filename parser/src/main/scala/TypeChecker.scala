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
			case x => (x, symbol)
		}
	}

	//TODO: augment func out of while

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
						val list: List[Tree] = iterateBlock(List(body))
						list.reverse match {
							case x :: xs => xs ++ List(assignNode(id, x, deff, idx, ns))
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

	/*

	def calculateScopeAllocSize(blockBody: List[Tree], symbol: HashMap[String, String]): List[Tree] = {
		var idx = 0
		val newbody = blockBody.map {
			case assignNode(id, valueNode(name, "actualstring"), deff, _, ns) =>
				val ret = assignNode(id, valueNode(name, "actualstring"), deff, idx, ns)
				idx += name.length - 2
				ret
			case assignNode(id, binopNode(l, r, o, "string"), deff, _, ns) =>
				val sizel: Int = l match {
					case valueNode(n, "actualstring") => n.length - 2
					case _ => (symbol.get(id)).toString.toInt
				}
				val sizer: Int = r match {
					case valueNode(n, "actualstring") => n.length - 2
					case _ => (symbol.get(id)).toString.toInt
				}
				val ret = assignNode(id, binopNode(l, r, o, "string"), deff, idx, ns)
				idx += sizel + sizer
			case x => x
		}
		val allocName = Util.genRandomName()
		var ret = List[Tree](allocNode(allocName, idx))
		newbody.foreach {
			case returnNode(body, ns) => ret ++= List(freeNode(allocName)) ++ List(returnNode(body, ns))
			case x: Tree => ret ++= List(x)
		}
		ret
	}

*/

	def annotateChildren(children: List[Tree], symbol: HashMap[String, Int]): List[Tree] = {
		var symClone = symbol
		var tmpIdx = 0

		//iter children
		val newchildren = children.map(e => {
			val (newElem, sym, size) = annotateSize(e, symClone, tmpIdx)
			symClone = symClone ++ sym
			tmpIdx += size
			newElem
		})

		//malloc culmultative size from above
		val allocName = Util.genRandomName()
		var ret = List[Tree](allocNode(allocName, tmpIdx))

		//contains return? free before
		var foundReturn = false
		newchildren.foreach {
			case returnNode(body, ns) =>
				ret ++= List(freeNode(allocName)) ++ List(returnNode(body, ns))
				foundReturn = true
			case x: Tree => ret ++= List(x)
		}

		//no return? insert free anyway
		if (!foundReturn) {
			ret ++= List(freeNode(allocName))
		}
		ret
	}

	def annotateSize(AST: Tree, symbol: HashMap[String, Int] = HashMap(), idx: Int = 0): (Tree, HashMap[String, Int], Int) = {
		AST match {
			case assignNode(id, valueNode(name, "actualstring"), deff, _, _) =>
				val size = name.length - 2
				val ret = assignNode(id, valueNode(name, "actualstring"), deff, idx, "string")
				(ret, symbol + (id -> size), size)

			case assignNode(id, binopNode(numbers, ops, _, ns), deff, _, "string") =>
				val allsizes = numbers.map {
					case valueNode(n, "string") => symbol.getOrElse(n, 0)
					case _ => 0
				}.sum
				val ret = assignNode(id, binopNode(numbers, ops, idx, ns), deff, 0, "string")
				(ret, symbol + (id -> allsizes), allsizes)

			case assignNode(id, functionNode(_, args, blockNode(children, ns), fns), deff, idx, ans) =>
				val newchildren = annotateChildren(children, symbol)
				(functionNode(id, args, blockNode(newchildren, ns), fns), symbol, 0)
			case blockNode(children, ns) =>
				val newchildren = annotateChildren(children, symbol)
				(blockNode(newchildren, ns), symbol, 0)
			case ifNode(cond, body, els, ns) =>
				val newbody = body match {
					case blockNode(children, ns) => blockNode(annotateChildren(children, symbol), ns)
					case x => x
				}
				val newels = els match {
					case Some(blockNode(children, ns)) => Some(blockNode(annotateChildren(children, symbol), ns))
					case x => x
				}
				(ifNode(cond, newbody, newels, ns), symbol, 0)
			case whileNode(cond, body, ns) =>
				(whileNode(cond, body, ns), symbol, 0)

			case x => (x, symbol, 0)
		}
	}

}