import Util.{TupleAddition, extractNode}

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

	def injectExternalMethods() : HashSet[String] = {
		HashSet() + "println"+"print"+"I"+"split"
	}

	//returns (captured variables, free variables)
	def findFreeVariables(AST: Tree, outerScope: HashSet[String]): (HashSet[String], mutable.LinkedHashSet[String]) = AST match {
		case wordNode(x) =>
			if (!outerScope.contains(x)) {
				(HashSet(), mutable.LinkedHashSet(x)) //free var
			} else if(outerScope.contains(x)) {
				(HashSet(x), mutable.LinkedHashSet()) //captured
			} else {
				(HashSet(), mutable.LinkedHashSet()) //neither captured or free
			}
		case unopNode(op, exp) => findFreeVariables(exp, outerScope)
		case binopNode(op, l, r) => findFreeVariables(l, outerScope) ++ findFreeVariables(r, outerScope)
		case assignNode(wordNode(id), body) => findFreeVariables(body, outerScope)
		//TODO: assign in one cell used in next
		case arrayNode(elem) => elem.map(findFreeVariables(_, outerScope)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
		case accessNode(arr, index) => findFreeVariables(arr, outerScope) ++ findFreeVariables(index, outerScope)
		case callNode(id, args) => findFreeVariables(id, outerScope) ++ findFreeVariables(arrayNode(args), outerScope)
		case blockNode(elems) =>
			var currentState = (HashSet[String](), mutable.LinkedHashSet[String]())
			elems.foreach(x => {
				currentState ++= findFreeVariables(x, outerScope)
			})
			currentState

		case ifNode(cond, body, elseBody, _) =>
			val freeInEls: (HashSet[String], mutable.LinkedHashSet[String]) = elseBody match {
				case Some(els) => findFreeVariables(els, outerScope)
				case None => (HashSet(), mutable.LinkedHashSet())
			}
			findFreeVariables(cond, outerScope) ++ findFreeVariables(body, outerScope) ++ freeInEls

		case mapNode(f, array) => findFreeVariables(array, outerScope)
		case comprehensionNode(body, wordNode(variable), array, Some(filter)) =>
			findFreeVariables(body, outerScope + variable) ++
				findFreeVariables(filter, outerScope + variable) ++
				findFreeVariables(array, outerScope)
		case comprehensionNode(body, wordNode(variable), array, None) => findFreeVariables(body, outerScope) ++ findFreeVariables(array, outerScope)
		case rangeNode(from,to) => findFreeVariables(from,outerScope) ++ findFreeVariables(to,outerScope)
		case integerNode(_) | boolNode(_) | stringNode(_) => (HashSet(), mutable.LinkedHashSet())
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

				var (capturedVars, freeVars) = findFreeVariables(blockNode(children), symbol++injectExternalMethods())
				if (topLevelBlock) {
					topLevelBlock = false
					freeVars = freeVars.empty
				}

				var cumulativeSymbol = freeVars.to(HashSet)

				val recursedChildren = children.map(x => {
					val (exp, localSym) = recurse(x, cumulativeSymbol)
					cumulativeSymbol ++= localSym
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
				(functionNode(capturedVars.toList.map(wordNode), freeVars.toList.map(wordNode), blockNode(childrenWithReturn), null), symbol)

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
					case wordNode(id) => functions.contains(id) match {
						case true => functions(id).captured
						case false => List() //TODO: self recursive functions cannot capture variables
					}
					case _ => List()
				}

				//TODO: handle captured variables if anon func
				val func: Tree = recurse(f, symbol)._1 match {
					case functionNode(fcap, fargs, fbody, meta) =>
						val (pre, newFunction) = Util.extractNode(lambdaNode(fcap, fargs, fbody))
						extractedNodes += pre
						newFunction
					case callNode(ff,fargs) =>
						val (pre, replace) = extractNode(callNode(ff,fargs))
						extractedNodes += pre
						replace
					case sequenceNode(nodes) =>
						val (pre, replace) = extractNode(sequenceNode(nodes))
						extractedNodes += pre
						replace
					case wordNode(x) => wordNode(x)
					case x => throw new NotImplementedError("cannot handle function being: "+x.toString)
				}
				val finalArgs = capturedArgs ++ nargs
				val sequence = extractedNodes.toList :+ callNode(func, finalArgs)
				if (sequence.length == 1) return (sequence.head,symbol)
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

			case rangeNode(from,to) => (rangeNode(recurse(from,symbol)._1,recurse(to,symbol)._1),symbol)

			case typedNode(exp, ty) => throw new NotActiveException(exp.toString)

			case x => (x, symbol)
		}
	}
}
