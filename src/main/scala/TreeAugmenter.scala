import Util.TupleAddition

import java.io.NotActiveException
import scala.collection.immutable.HashSet
import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TreeAugmenter extends Stage {


	var topLevelBlock = true

	//TODO
	val fixed = mutable.HashMap[String, ListBuffer[List[Type]]]()
	val functions = mutable.HashMap[String, functionNode]()

	def process(AST: Tree): Tree = {
		println("--------------------- augmenting --------------------")
		val (t, _) = recurse(AST, HashSet())
		t
	}

	//returns (used variables, unused variables)
	def findFreeVariables(AST: Tree, symbol: HashSet[String]): (HashSet[String], mutable.LinkedHashSet[String]) = AST match {
		case wordNode(x) =>
			if (symbol.contains(x)) {
				(HashSet(), mutable.LinkedHashSet())
			} else {
				(HashSet(), mutable.LinkedHashSet(x))
			}
		case boolNode(x) => (HashSet(), mutable.LinkedHashSet())
		case unopNode(op, exp) =>
			findFreeVariables(exp, symbol)
		case binopNode(op, l, r) => findFreeVariables(l, symbol) ++ findFreeVariables(r, symbol)
		case assignNode(wordNode(id), body) =>
			val (used, unsued) = findFreeVariables(body, symbol + id)
			(used ++ HashSet[String](id), unsued)
		//TODO: assign in one cell used in next
		case arrayNode(elem) => elem.map(findFreeVariables(_, symbol)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
		case accessNode(arr, index) => findFreeVariables(arr, symbol) ++ findFreeVariables(index, symbol)
		case callNode(id, args) => findFreeVariables(id, symbol) ++ findFreeVariables(arrayNode(args), symbol)
		case blockNode(elems) =>
			var currentState = (symbol, mutable.LinkedHashSet[String]())
			elems.foreach(x => {
				val variablesInUse = currentState._1
				currentState ++= findFreeVariables(x, variablesInUse)
			})
			currentState

		case ifNode(cond, body, elseBody, _) =>
			val freeInEls: (HashSet[String], mutable.LinkedHashSet[String]) = elseBody match {
				case Some(els) => findFreeVariables(els, symbol)
				case None => (HashSet(), mutable.LinkedHashSet())
			}
			findFreeVariables(cond, symbol) ++ findFreeVariables(body, symbol) ++ freeInEls

		case mapNode(f, array) => findFreeVariables(array, symbol)
		case comprehensionNode(body, wordNode(variable), array, Some(filter)) =>
			findFreeVariables(body, symbol + variable) ++
				findFreeVariables(filter, symbol + variable) ++
				findFreeVariables(array, symbol)
		case comprehensionNode(body, wordNode(variable), array, None) => findFreeVariables(body, symbol + variable) ++ findFreeVariables(array, symbol)
		case integerNode(_) | stringNode(_) => (HashSet(), mutable.LinkedHashSet())
		case x => throw new NotImplementedError("Could not match every case in unsued variables: " + x.toString)
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

				val (lpreassign, newleft) = Util.extractNode(left)
				val (rpreassign, newright) = Util.extractNode(right)

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
					case functionNode(captured, args, fbody, _) =>
						//give id to node
						//remove own id from captured variables.
						val noSelfCapture = captured.filter { case wordNode(elemId) => elemId != id }
						val node = functionNode(noSelfCapture, args, fbody, metaNode(id, null))
						functions += (id -> node)
						(node, newsym)
					case sequenceNode(l) =>
						val newBody = if (symbol.contains(id)) {
							reassignNode(wordNode(id), l.last)
						} else {
							assignNode(wordNode(id), l.last)
						}
						(sequenceNode(l.init :+ newBody), newsym)
					case _ =>
						val newBody = if (symbol.contains(id)) {
							reassignNode(wordNode(id), body)
						} else {
							assignNode(wordNode(id), body)
						}
						(newBody, newsym)
				}

			case reassignNode(wordNode(id), b) =>
				recurse(b, symbol)._1 match {
					case sequenceNode(l) =>
						(sequenceNode(l.init :+ reassignNode(wordNode(id), l.last)), symbol)
					case x =>
						(reassignNode(wordNode(id), x), symbol)
				}

			case blockNode(children) =>
				if (children.isEmpty)
					return (functionNode(List(), List(), blockNode(List(returnNode(integerNode(0)))), null), symbol)

				//TODO scopeVars. add fixed vars list to func node
				var (scopeVars, freeVars) = findFreeVariables(blockNode(children), symbol)
				if (topLevelBlock) {
					topLevelBlock = false
					freeVars = freeVars.empty
				}

				var culumativeSymbol = freeVars.to(HashSet)

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
				(functionNode(scopeVars.toList.map(wordNode), freeVars.toList.map(wordNode), blockNode(childrenWithReturn), null), symbol)

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

				//variables that are captured comes before the real arguments
				val capturedArgs = f match {
					case wordNode(id) =>
						functions.contains(id) match {
							case true => functions(id).captured
							case false => List() //TODO: self recursive functions cannot capture variables
						}
					case _ => throw new NotImplementedError("fix captured variables for anon func")
				}

				//TODO: handle captured variables if anon func
				val func: Tree = recurse(f, symbol)._1 match {
					case functionNode(fcap, fargs, fbody, meta) =>
						val (pre, newFunction) = Util.extractNode(lambdaNode(fcap, fargs, fbody))
						extractedNodes += pre
						newFunction
					case x => x
				}
				val finalArgs = capturedArgs ++ nargs
				val sequence = extractedNodes.toList :+ callNode(func, finalArgs)
				(sequenceNode(sequence), symbol)

			case arrayNode(elements) =>
				val extractedNodes = ListBuffer[Tree]()
				var sym = symbol
				val children = elements.map(x => {
					val (elem, s) = recurse(x, sym)
					sym = s
					elem
				}).map {
					case assignNode(wordNode(id), body) =>
						extractedNodes += assignNode(wordNode(id),body)
						wordNode(id)
					case x => x
				}
				(sequenceNode(extractedNodes.toList:+arrayNode(children)), sym)

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
					case exp => recurse(exp, symbol)._1 match {
						case sequenceNode(l) => blockNode(l.init :+ reassignNode(wordNode(id), l.last))
						case x => blockNode(List(reassignNode(wordNode(id), x)))
					}


				}
				val nels = elseBody match {
					case None => None
					case Some(x) =>
						//TODO register sym as above
						recurse(x, symbol)._1 match {
							case blockNode(elem) =>
								val (blockNode(elems), _) = recurse(blockNode(elem), symbol)
								Some(blockNode(elems.init :+ reassignNode(wordNode(id), elems.last)))
							case exp => recurse(exp, symbol)._1 match {
								case sequenceNode(l) => Some(blockNode(l.init :+ reassignNode(wordNode(id), l.last)))
								case x => Some(blockNode(List(reassignNode(wordNode(id), x))))
							}
						}
				}
				val result = wordNode(id)
				val sequence = preIf :+ ifNode(newCond, newbody, nels, id) :+ result
				(sequenceNode(sequence.toList), sym)


			case mapNode(f, array) =>
				val newF = recurse(f, symbol) match {
					case (functionNode(a, b, c, d), _) => lambdaNode(a, b, c)
					case (x, _) => x
				}
				(mapNode(newF, recurse(array, symbol)._1), symbol)

			case comprehensionNode(body, variable, array, filter) =>
				val bodyF = recurse(blockNode(List(body)), symbol)._1 match {
					case functionNode(cap, args, body, _) => lambdaNode(cap, args, body)
				}
				val newFilter = filter match {
					case Some(f) => recurse(blockNode(List(f)), symbol)._1 match {
						case functionNode(cap, args, body, _) => Some(lambdaNode(cap, args, body))
					}
					case None => None
				}
				val result = comprehensionNode(bodyF, variable, array, newFilter)
				(result, symbol)

			case typedNode(exp, ty) => throw new NotActiveException(exp.toString)

			case x => (x, symbol)
		}
	}
}
