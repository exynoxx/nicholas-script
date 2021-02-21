import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TreeAugmenter {

	var functionArgumentTypes = mutable.HashMap[String, List[String]]()
	var globalFunctionExtracts = ListBuffer[Tree]()

	def castToFunction(ty: String): String = {
		ty match {
			case "string" => "toString"
			case "int" => "toInt"
			case "bool" => "toBool"
		}
	}

	def autoCastElement(e: Tree, targetType: String): Tree = {
		if (targetType == "void") {
			return e
		}
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

					case assignNode(id, body, deff, idx, ns) =>

						val stringID = id match {
							case valueNode(v, _) => v
							case x => x.toString
						}

						//body might append multiple elem infront.
						// last element is actual body
						val list: List[Tree] = iterateBlock(List(body))
						list.reverse match {
							case x :: xs => x match {
								//assign of a function is just a function for the backend languages
								case functionNode(_, _, _, _) => List(x)
								case objectNode(_, _, _) => List(x)++xs
								case lineNode(_, _) => xs ++ List(assignNode(id, x, deff, idx, ns))
								case _ =>
									val newX = autoCastElement(x, ns)
									xs ++ List(assignNode(id, newX, deff, idx, ns))
							}
						}

					case functionNode(id, args, body, ns) =>
						functionArgumentTypes += (id -> args.map {
							case argNode(name, Util.functionTypePattern(_, _)) => "void"
							case argNode(name, ty) => ty
						})
						args.foreach {
							case argNode(fid, ty) => {
								if (Util.functionTypePattern.matches(ty)) {
									val list = Util.functionTypePattern.findAllIn(ty).group(1).split(",")
									functionArgumentTypes += (fid -> list.toList)
								}
							}
							case _ =>
						}

						val fbody = iterateBlock(List(body))(0)
						val retbody = fbody match {
							case x =>
								val tmp = returnNode(x, x.nstype)
								blockNode(List(tmp), ns)

						}
						List(functionNode(id, args, retbody, ns))
					case blockNode(children, ns) =>
						val newchildren = iterateBlock(children)
						List(blockNode(newchildren, ns))

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

						val autoTypeArgs = functionArgumentTypes.get(id) match {
							case Some(l) => newargs.zip(l).map {
								case (elem, ty) => {
									autoCastElement(elem, ty)
								}
							}
							case None => newargs
						}
						tmpList += callNode(id, autoTypeArgs, deff, ns)
						tmpList.toList

					case returnNode(functionNode("ret", args, body, ty), ns) =>
						val id = Util.genRandomName()
						globalFunctionExtracts += functionNode(id, args, body, ty)
						List(returnNode(valueNode(id, ty), ty))

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

					case anonNode(args, body, ns) =>
						val randomName = Util.genRandomName()
						val replacement = functionNode(randomName, args, body, ns)

						var retList: ListBuffer[Tree] = ListBuffer()
						retList += iterateBlock(List(replacement))(0)
						retList += valueNode(randomName, ns)
						retList.toList

					case objectNode(id, rows, ns) =>
						var retList: ListBuffer[Tree] = ListBuffer()
						var funcs: ListBuffer[Tree] = ListBuffer()
						val newrows = rows.filter {
							case functionNode(name, args, body, ty) =>
								funcs += functionNode(name, List(specialArgNode("&mut self", null)) ++ args, body, ty)
								false
							case x => true
						}
						retList += objectAssociatedFunctionNode(id, funcs.toList, null)
						retList += objectNode(id, newrows, ns)
						retList.toList

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
				blockNode(newchildren ++ globalFunctionExtracts.toList, ns)
			}
			case _ => println("error")
				nullLeaf()
		}
	}
}
