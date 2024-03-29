package old

import scala.collection.immutable.HashMap
import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TreeAugmenter {

	var globalFunctionExtracts = ListBuffer[Tree]()

	def iterateBlock(blockBody: List[Tree], objSymbol: HashMap[String, Type]): List[Tree] = {
		blockBody match {
			case x :: xs => {
				val ret: List[Tree] = x match {

					case valueNode(name, ns) =>
						objSymbol.contains(name) match {
							case true => List(valueNode("self." + name, ns))
							case false => List(valueNode(name, ns))
						}

					case binopNode(numbers, ops, idx, ns) =>
						val newNumbers = numbers.map(e => iterateBlock(List(e), objSymbol)(0))
						List(binopNode(newNumbers, ops, idx, ns))


					case assignNode(id, body, deff, idx, ns) =>

						//body might append multiple elem infront.
						// last element is actual body
						val list: List[Tree] = iterateBlock(List(body), objSymbol)
						list.reverse match {
							case x :: xs => x match {
								//assign of a function is just a function for the backend languages
								case functionNode(_, _, _, _) => List(x)
								case objectNode(_, _, _) => List(x) ++ xs
								case lineNode(_, _) => xs ++ List(assignNode(id, x, deff, idx, ns))
								case _ =>
									xs ++ List(assignNode(id, x, deff, idx, ns))
							}
						}

					case functionNode(id, args, body, ns) =>
						val fbody = iterateBlock(List(body), objSymbol).head
						List(functionNode(id, args, fbody, ns))

					case blockNode(children, ns) =>
						val newchildren = iterateBlock(children, objSymbol)
						List(blockNode(newchildren, ns))

					case ifNode(c, b, els, ns) =>
						val ifbody = iterateBlock(List(b), objSymbol).head
						val elsbody = els match {
							case Some(els) => Some(iterateBlock(List(els), objSymbol).head)
							case None => None
						}
						List(ifNode(c, ifbody, elsbody, b.ty))

					case whileNode(c, b, ns) =>
						List(whileNode(c, iterateBlock(List(b), objSymbol).head, ns))

					case forNode(v, a, b, ns) =>
						val tmpList = ListBuffer[Tree]()
						val arrList = iterateBlock(List(a), objSymbol)
						val newa: Tree = arrList.reverse match {
							case x :: xs =>
								tmpList ++= xs
								x match {
									case arrayNode(elem, ty) =>
										val newname = Util.genRandomName()
										tmpList += assignNode(valueNode(newname, ty), arrayNode(elem, ty), true, 0, ty)
										valueNode(newname, ty)
									case x => x
								}
						}

						tmpList ++= List(forNode(v, newa, iterateBlock(List(b), objSymbol).head, ns))
						tmpList.toList

					case callNode(id, args, deff, ns) =>
						val tmpList = ListBuffer[Tree]()

						val newargs = args.map { x =>
							val retList: List[Tree] = iterateBlock(List(x), objSymbol)
							val retElement: Tree = retList.reverse match {
								case x :: xs =>
									tmpList ++= xs
									x
							}
							retElement
						}

						tmpList += callNode(id, newargs, deff, ns)
						tmpList.toList

					case returnNode(functionNode("ret", args, body, ty), ns) =>
						val id = Util.genRandomName()
						globalFunctionExtracts += functionNode(id, args, body, ty)
						List(returnNode(valueNode(id, ty), ty))

					case arrayNode(elem, ns) =>
						val tmpList = ListBuffer[Tree]()
						val newelem = elem.map { x =>
							val retList: List[Tree] = iterateBlock(List(x), objSymbol)
							val retElement: Tree = retList.reverse match {
								case y :: ys =>
									tmpList ++= ys
									y
							}
							retElement
						}
						tmpList += arrayNode(newelem, ns)
						tmpList.toList
					case rangeNode(from, to, ns) =>
						val tmpList = ListBuffer[Tree]()
						val extract = (x: Tree) => {
							x match {
								case valueNode(v, vns) =>
									valueNode(v, vns)
								case y =>
									val id = Util.genRandomName()
									val assign = assignNode(valueNode(id, ns), y, true, 0, intType(null))
									val valn = valueNode(id, intType(null))
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
						val list = iterateBlock(List(index), objSymbol).reverse
						val newIndex: Tree = list match {
							case x :: xs => {
								preList ++= xs
								x
							}
						}

						preList += accessNode(id, newIndex, ns)
						preList.toList

					case anonNode(args, body, ns) =>
						val randomName = Util.genRandomName()
						val replacement = functionNode(randomName, args, body, ns)

						var retList: ListBuffer[Tree] = ListBuffer()
						retList += iterateBlock(List(replacement), objSymbol).head
						retList += valueNode(randomName, ns)
						retList.toList

					case objectNode(id, rows, ns) =>
						var retList: ListBuffer[Tree] = ListBuffer()
						var funcs: ListBuffer[Tree] = ListBuffer()
						var overrides: ListBuffer[Tree] = ListBuffer()

						var newObjSymb = mutable.HashMap[String,Type]()

						val newrows = rows.filter {
							case functionNode(name, args, body, ty) =>
								funcs += functionNode(name, List(specialArgNode("&mut self", null)) ++ args, body, ty)
								false
							case overrideNode(op, f, ns) =>
								val augmentedF = iterateBlock(List(f), newObjSymb.to(HashMap)).head
								overrides += overrideNode(op, augmentedF, ns)
								false
							case objectElementNode(name, ty) =>
								newObjSymb += (name -> ty)
								true
							case x => true
						}


						val augmentedFuncs = iterateBlock(funcs.toList, newObjSymb.to(HashMap))
						retList += objectAssociatedFunctionNode(id, augmentedFuncs, null)
						retList ++= overrides
						retList += objectNode(id, newrows, ns)
						retList.toList

					case t => List(t)
				}

				ret ++ iterateBlock(xs, objSymbol)
			}
			case _ => List()
		}
	}

	//TODO string input in func annotate correct size at runtime
	def augment(AST: Tree): Tree = {
		AST match {
			case blockNode(children, ns) => {
				val newchildren = iterateBlock(children, HashMap())
				blockNode(newchildren ++ globalFunctionExtracts.toList, ns)
			}
			case _ => println("error")
				nullLeaf()
		}
	}
}
