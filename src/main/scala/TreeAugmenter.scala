import java.io.NotActiveException
import scala.collection.immutable.{HashMap, HashSet}
import scala.collection.mutable
import scala.collection.mutable.ListBuffer
import scala.util.control.Exception

class TreeAugmenter extends Stage {

	def process(AST: Tree): Tree = {
		println("--------------------- augmenting --------------------")
		val (t, _) = recurse(AST, HashSet())
		t
	}

	//returns (used variables, unused variables)
	def findUnusedVariables(AST: Tree, symbol: HashSet[String]): (HashSet[String], mutable.LinkedHashSet[String]) = AST match {
		case wordNode(x) =>
			if (symbol.contains(x)) {
				(HashSet(), mutable.LinkedHashSet())
			} else {
				(HashSet(), mutable.LinkedHashSet(x))
			}
		case boolNode(x) => (HashSet(), mutable.LinkedHashSet())
		case unopNode(op, exp) =>
			findUnusedVariables(exp, symbol)
		case binopNode(op, l, r) =>
			val (lused, lunused) = findUnusedVariables(l, symbol)
			val (rused, runused) = findUnusedVariables(r, symbol)
			(lused ++ rused, lunused ++ runused)
		case assignNode(wordNode(id), body) =>
			val (used, unsued) = findUnusedVariables(body, symbol + id)
			(used ++ HashSet[String](id), unsued)
		//TODO: assign in one cell used in next
		case arrayNode(elem) =>
			elem.map(findUnusedVariables(_, symbol)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
		case accessNode(arr, index) =>
			val (u, unu) = findUnusedVariables(arr, symbol)
			val (u1, unu1) = findUnusedVariables(index, symbol)
			(u ++ u1, unu ++ unu1)
		//case callNode(blockNode(elems), args) => ()
		case callNode(id, args) =>
			val (u, unu) = args.map(findUnusedVariables(_, symbol)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
			val (u_id, unu_id) = findUnusedVariables(id, symbol)
			(u ++ u_id, unu ++ unu_id)
		case blockNode(elems) =>
			var (used, unused) = (symbol, mutable.LinkedHashSet[String]())
			for (x <- elems) {
				val (u, unu) = findUnusedVariables(x, used)
				used ++= u
				unused ++= unu
			}
			(used, unused)
		case ifNode(cond, body, elseBody, _) =>
			val (u1, unu1) = findUnusedVariables(cond, symbol)
			val (u2, unu2) = findUnusedVariables(body, symbol)
			val (u3, unu3) = elseBody match {
				case Some(els) => findUnusedVariables(els, symbol)
				case None => (HashSet(), mutable.LinkedHashSet())
			}
			(u1 ++ u2 ++ u3, unu1 ++ unu2 ++ unu3)
		case mapNode(f, array) => findUnusedVariables(array, symbol)
		case comprehensionNode(body, wordNode(variable), array, Some(filter)) =>
			val (u1, unu1) = findUnusedVariables(body, symbol + variable)
			val (u2, unu2) = findUnusedVariables(filter, symbol + variable)
			val (u3, unu3) = findUnusedVariables(array, symbol)
			(u1 ++ u2 ++ u3, unu1 ++ unu2 ++ unu3)
		case comprehensionNode(body, wordNode(variable), array, None) =>
			val (u1, unu1) = findUnusedVariables(body, symbol + variable)
			val (u2, unu2) = findUnusedVariables(array, symbol)
			(u1 ++ u2, unu1 ++ unu2)
		case integerNode(_) | stringNode(_) => (HashSet(), mutable.LinkedHashSet())

		case x => throw new NotImplementedError("Could not match every case in unsued variables: " + x.toString)
	}

	def extractNode(node: Tree): (Tree, Tree) = node match {
		case sequenceNode(list) =>
			(sequenceNode(list.init), list.last)
		case x =>
			val id = Util.genRandomName()
			val assign = assignNode(wordNode(id), x)
			(assign, wordNode(id))
	}

	def recurse(AST: Tree, symbol: HashSet[String]): (Tree, HashSet[String]) = {
		AST match {
			case wordNode(x) =>
				if (!symbol.contains(x)) return (wordNode(x), symbol + x)
				(wordNode(x), symbol)

			case unopNode(op, exp) =>
				val (body, sym) = recurse(exp, symbol)
				(unopNode(op, body), sym)

			case binopNode(op, l, r) =>
				val (left, ls) = recurse(l, symbol)
				val (right, rs) = recurse(r, symbol)

				val (lpreassign, newleft) = extractNode(left)
				val (rpreassign, newright) = extractNode(right)

				val preassign = ListBuffer[Tree]()

				preassign ++= (lpreassign match {
					case sequenceNode(l) => l
					case x => List(x)
				})

				preassign ++= (rpreassign match {
					case sequenceNode(l) => l
					case x => List(x)
				})

				val ret = binopNode(op, newleft, newright)
				preassign += ret
				(sequenceNode(preassign.toList), symbol ++ ls ++ rs)


			case assignNode(wordNode(id), b) =>
				//TODO: if functionNode and arg has function type in scope. convert from wordNode to call node and capture
				//remaining args
				val (body, sym) = recurse(b, symbol + id)

				val newsym = symbol ++ sym + id //maybe redundant

				body match {
					case functionNode(args, fbody, _) =>
						(functionNode(args, fbody, metaNode(id, null)), newsym)
					case _ => if (symbol.contains(id)) {
						(reassignNode(wordNode(id), body), newsym)
					} else {
						(assignNode(wordNode(id), body), newsym)
					}
				}

			case reassignNode(wordNode(id),b) =>
				recurse(b,symbol)._1 match {
					case sequenceNode(l) =>
						(sequenceNode(l.init :+ reassignNode(wordNode(id),l.last)),symbol)
					case x =>
						(reassignNode(wordNode(id),x),symbol)
				}

			case blockNode(children) =>
				if (children.isEmpty)
					return (functionNode(List(), blockNode(List(returnNode(integerNode(0)))), null), symbol)

				val (_, unusedVariables) = findUnusedVariables(blockNode(children), HashSet())

				var culumativeSymbol = unusedVariables.to(HashSet)

				val recursedChildren = children.map(x => {
					val (exp, localSym) = recurse(x, culumativeSymbol)
					culumativeSymbol ++= localSym
					exp
				}).flatMap {
					case sequenceNode(l) => l
					//case blockNode(l) => l
					case x => List(x)
				}

				val childrenWithReturn = recursedChildren.last match {
					case assignNode(_, _) => recursedChildren ++ List(returnNode(integerNode(0)))
					case reassignNode(_, _) => recursedChildren ++ List(returnNode(integerNode(0)))
					case _ => recursedChildren.init :+ returnNode(recursedChildren.last)
				}
				(functionNode(unusedVariables.toList.map(wordNode), blockNode(childrenWithReturn), null), symbol)

			case callNode(f, args) =>
				//TODO symbol register each arg if contain assign
				val extractedNodes = ListBuffer[Tree]()
				val nargs = args.map(x => recurse(x, symbol)._1 match {
					case sequenceNode(list) =>
						extractedNodes ++= list.init
						list.last
					case mapNode(f, array) =>
						val id = Util.genRandomName()
						extractedNodes += assignNode(wordNode(id), f)
						mapNode(wordNode(id), array)
					case y => y
				})

				val func: Tree = recurse(f, symbol)._1 match {
					case functionNode(fargs, fbody, meta) =>
						val (pre,newFunction) = extractNode(functionNode(fargs, fbody, meta))
						extractedNodes += pre
						newFunction
					case x => x
				}

				val sequence = extractedNodes.toList :+ callNode(func, nargs)
				(sequenceNode(sequence), symbol)

			case arrayNode(elements) =>
				var sym = symbol
				val children = elements.map(x => {
					val (elem, s) = recurse(x, sym)
					sym = s
					elem
				})
				(arrayNode(children), sym)

			case accessNode(arrayId, idx) =>
				val (index, sym) = recurse(idx, symbol)
				(accessNode(arrayId, index), symbol ++ sym)


			case ifNode(cond, body, elseBody, _) =>
				//TODO: impl returns as "#=1+1"

				val preIf = ListBuffer[Tree]()
				val id = Util.genRandomName()
				var sym = symbol
				val newCond = recurse(cond, symbol)._1 match {
					case sequenceNode(l) =>
						preIf ++= l.init
						l.last
					case x => x
				}
				val newbody = body match {
					case blockNode(elem) =>
						val (blockNode(elems), localSym) = recurse(blockNode(elem), symbol)
						sym ++= localSym
						blockNode(elems.init :+ reassignNode(wordNode(id), elems.last))
					case exp =>
						val b = recurse(exp, symbol)._1 match {
							case sequenceNode(l) =>
								preIf ++= l.init
								l.last
							case x => x
						}
						blockNode(List(reassignNode(wordNode(id), b)))

				}
				val nels = elseBody match {
					case None => None
					case Some(x) =>
						//TODO register sym as above
						recurse(x, symbol)._1 match {
							case blockNode(elem) =>
								val (blockNode(elems), _) = recurse(blockNode(elem), symbol)
								Some(blockNode(elems.init :+ reassignNode(wordNode(id), elems.last)))
							case exp =>
								val b = recurse(exp, symbol)._1 match {
									case sequenceNode(l) =>
										preIf ++= l.init
										l.last
									case x => x
								}
								Some(blockNode(List(reassignNode(wordNode(id), b))))

						}
				}
				val result = wordNode(id)
				//preIf += assignNode(result, integerNode(0)) this is done in codeGen
				val sequence = preIf :+ ifNode(newCond, newbody, nels, id) :+ result
				(sequenceNode(sequence.toList), sym)


			case mapNode(f, array) =>
				val retNode = recurse(f, symbol) match {
					//if anon func: give it a name and replace with wordNode
					case (functionNode(a, b, c), _) =>
						val (pre,newFunction) = extractNode(functionNode(a, b, c))
						val map = mapNode(newFunction, recurse(array, symbol)._1)
						sequenceNode(List(pre, map))
					case (x, _) => mapNode(x, recurse(array, symbol)._1)
				}
				(retNode, symbol)

			case comprehensionNode(body, variable, array, filter) =>
				val bodyId = Util.genRandomName()
				val bodyF = recurse(blockNode(List(body)), symbol)._1 match {
					case functionNode(args,body,_) => functionNode(args,body,metaNode(bodyId, null))
				}

				filter match {
					case Some(f) =>
						val filterId = Util.genRandomName()
						val filterF = recurse(blockNode(List(f)), symbol)._1 match {
							case functionNode(args,body,_) => functionNode(args,body,metaNode(filterId, null))
						}
						val result = comprehensionNode(wordNode(bodyId), variable, array, Some(wordNode(filterId)))
						(sequenceNode(List(bodyF, filterF, result)), symbol)

					case None =>
						val result = comprehensionNode(wordNode(bodyId), variable, array, None)
						(sequenceNode(List(bodyF, result)), symbol)
				}

			case typedNode(exp, ty) => throw new NotActiveException(exp.toString)

			case x => (x, symbol)
		}
	}
}
