import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TreeAugmenter {

	var functionArgumentTypes = mutable.HashMap[String, List[String]]()

	def castToFunction(ty: String): String = {
		ty match {
			case "string" => "toString"
			case "int" => "toInt"
			case "bool" => "toBool"
		}
	}

	def autoCastElement(e: Tree, targetType: String): Tree = {
		val eType = e.nstype match {
			case "actualstring" => "string"
			case x => x
		}
		eType match {
			case `targetType` => e
			case Util.arrayTypePattern(ty) => e
			case _ =>
				val fCallName = castToFunction(targetType)
				callNode(fCallName, List(e), false, targetType)
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
								case lineNode(_, _) => xs ++ List(assignNode(id, x, deff, idx, ns))
								case _ =>
									val newX = autoCastElement(x, ns)
									xs ++ List(assignNode(id, newX, deff, idx, ns))
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
						functionArgumentTypes += (id -> args.map { case argNode(name, ty) => ty })
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
					case forNode(v, a, b, ns) =>
						val tmpList = ListBuffer[Tree]()
						val arrList = iterateBlock(List(a))
						val newa: Tree = arrList.reverse match {
							case x :: xs =>
								tmpList ++= xs
								x match {
									case arrayNode(elem, Util.arrayTypePattern(ty)) =>
										val newname = Util.genRandomName()
										tmpList += assignNode(valueNode(newname, ty), arrayNode(elem, ty), true, 0, "array(" + ty + ")")
										valueNode(newname, ty)
									case x => x
								}
						}

						tmpList ++= List(forNode(v, newa, iterateBlock(List(b))(0), ns))
						tmpList.toList

					case callNode(id, args, deff, ns) =>
						val tmpList = ListBuffer[Tree]()

						val newargs = args.map { x =>
							val retList: List[Tree] = iterateBlock(List(x))
							val retElement: Tree = retList.reverse match {
								case x :: xs =>
									tmpList ++= xs
									x
							}
							retElement
						}
						val argsTy = newargs.zip(functionArgumentTypes(id))
						val autoTypeArgs = argsTy.map {
							case (elem, ty) => {
								autoCastElement(elem, ty)
							}
						}
						tmpList += callNode(id, autoTypeArgs, deff, ns)
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
					case arrayNode(elem, Util.arrayTypePattern(ns)) =>
						val tmpList = ListBuffer[Tree]()
						val newelem = elem.map { x =>
							val retList: List[Tree] = iterateBlock(List(x))
							val retElement: Tree = retList.reverse match {
								case y :: ys =>
									tmpList ++= ys
									y
							}
							autoCastElement(retElement, ns)
						}
						tmpList += arrayNode(newelem, "array(" + ns + ")")
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
						var preList: ListBuffer[Tree] = ListBuffer()
						val list = iterateBlock(List(index)).reverse
						val newIndex: Tree = list match {
							case x :: xs => {
								preList ++= xs
								autoCastElement(x, "int")
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
