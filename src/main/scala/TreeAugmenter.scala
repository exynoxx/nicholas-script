import java.io.NotActiveException
import scala.collection.immutable.{HashMap, HashSet}
import scala.collection.mutable
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

		case mapNode(f, array) => findUnusedVariables(array,symbol)
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
				(binopNode(op, left, right), symbol ++ ls ++ rs)


			case assignNode(wordNode(id), b) =>
				//TODO: if functionNode and arg has function type in scope. convert from wordNode to call node and capture
				//remaining args
				val (body, sym) = recurse(b, symbol + id)

				val newsym = symbol ++ sym + id //maybe redundant

				val newbody = body match {
					case functionNode(args, fbody, _) => functionNode(args, fbody, metaNode(id, null))
					case _ => body
				}

				symbol.contains(id) match {
					case true => (reassignNode(wordNode(id), newbody), newsym)
					case false => (assignNode(wordNode(id), newbody), newsym)
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
				val nargs = args.map(x => recurse(x, symbol)._1)
				val (func, sym) = recurse(f, symbol)
				(callNode(func, nargs), sym)


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
				val id = Util.genRandomName()
				var sym = symbol
				val (newCond, csym) = recurse(cond, symbol)
				sym ++= csym
				val newbody = body match {
					case blockNode(elem) =>
						val (blockNode(elems), localSym) = recurse(blockNode(elem), symbol)
						sym ++= localSym
						blockNode(elems.init :+ reassignNode(wordNode(id), elems.last))
					case exp =>
						val (b, localSym) = recurse(exp, symbol)
						sym ++= localSym
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
								val (b, _) = recurse(exp, symbol)
								Some(blockNode(List(reassignNode(wordNode(id), b))))
						}
				}
				val result = wordNode(id)
				val resultInit = assignNode(result, integerNode(0))
				(sequenceNode(List(resultInit, ifNode(newCond, newbody, nels, id), result)), sym)


			case mapNode(f, array) =>
				val retNode = recurse(f, symbol) match {
					//if anon func: give it a name and replace with wordNode
					case (functionNode(a,b,_),_) =>
						val id = Util.genRandomName()
						val preassign = assignNode(wordNode(id),functionNode(a,b,metaNode(id,null)))
						val map = mapNode(wordNode(id), recurse(array, symbol)._1)
						sequenceNode(List(preassign,map))

					case (x,_)=>mapNode(x, recurse(array, symbol)._1)
				}
				(retNode, symbol)


			case typedNode(exp, ty) => throw new NotActiveException(exp.toString)

			case x => (x, symbol)
		}
	}
}
