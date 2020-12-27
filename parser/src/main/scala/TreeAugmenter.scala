import scala.collection.mutable.ListBuffer

class TreeAugmenter {

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
								retList += assignNode(valueNode(n, "string"), tmp, true, 0, "actualstring")
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
								val tmpAssign = assignNode(valueNode(idName, ns), binopNode(l, r, o, ns), true, 0, ns)
								val tmp = returnNode(valueNode(idName, ns), ns)
								blockNode(List(tmpAssign, tmp), ns)
							case valueNode(value, ns) =>
								val tmp = returnNode(valueNode(value, ns), ns)
								blockNode(List(tmp), ns)
							case x =>
								val tmp = returnNode(x, x.nstype)
								blockNode(List(tmp), ns)

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
							/*case valueNode(nm, "actualstring") =>
								val n = Util.genRandomName()
								val ns = "actualstring"
								val preassign = assignNode(valueNode(n, ns), valueNode(nm, ns), true, 0, ns)
								val replaceElement = valueNode(n, "string")
								tmpList += preassign
								replaceElement*/
							case x =>
								val retList: List[Tree] = iterateBlock(List(x))
								val retElement: Tree = retList.reverse match {
									case x :: xs =>
										tmpList ++= xs
										x
								}
								retElement

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
							val preassign = assignNode(valueNode(id, ns), body, true, 0, ns)
							val replaceElement = valueNode(id, ns)
							preassign :: List(returnNode(replaceElement, ns))
						} else {
							List(returnNode(body, ns))
						}
					case arrayNode(elem, ns) =>
						val tmpList = ListBuffer[Tree]()
						val newelem = elem;
						/*val newelem = elem.map {
							case valueNode(value, ns) => valueNode(value, ns)
							case binopNode(numbers, ops, idx, ns) => binopNode(numbers, ops, idx, ns)
							case x =>
								val id = Util.genRandomName()
								val preassign = assignNode(valueNode(id, ns), x, true, 0, x.nstype)
								val replaceElement = valueNode(id, x.nstype)
								tmpList += preassign
								replaceElement
						}*/
						val newTy = newelem.head.nstype match {
							case "actualstring" => "array(string)"
							case "actualint" => "array(int)"
							case x => "array("+x+")"
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
									val assign = assignNode(valueNode(id, ns), y, true, 0, "int")
									val valn = valueNode(id, "int")
									tmpList += assign
									valn
							}
						}
						val newfrom = extract(from)
						val newto = extract(to)
						tmpList += rangeNode(newfrom, newto, ns)
						tmpList.toList
					case accessNode(id, index, ns) =>
						var preList:ListBuffer[Tree] = ListBuffer()
						val list = iterateBlock(List(index)).reverse
						val newIndex:Tree = list match {
							case x::xs => {
								preList ++= xs
								x
							}
						}

						preList += accessNode(id, newIndex, ns)
						preList.toList


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
