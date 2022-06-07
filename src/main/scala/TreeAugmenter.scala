import scala.collection.immutable.{HashMap, HashSet}
import scala.collection.mutable
import scala.util.control.Exception

class TreeAugmenter extends Stage{

	def process(AST: Tree): Tree = {
		println("--------------------- augmenting --------------------")
		val (t, _, _) = recurse(AST, unknownType(), HashMap("+" -> intType()))
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
			val (used, unsued) = findUnusedVariables(body, symbol+id)
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
		case ifNode(cond, body, elseBody,_) =>
			val (u1, unu1) = findUnusedVariables(cond, symbol)
			val (u2, unu2) = findUnusedVariables(body, symbol)
			val (u3, unu3) = elseBody match {
				case Some(els) => findUnusedVariables(els, symbol)
				case None => (HashSet(), mutable.LinkedHashSet())
			}
			(u1 ++ u2 ++ u3, unu1 ++ unu2 ++ unu3)

		case _ => (HashSet[String](), mutable.LinkedHashSet[String]())
	}

	def recurse(AST: Tree, typ:Type, symbol: HashMap[String, Type]): (Tree, Type, HashMap[String, Type]) = {
		AST match {
			case wordNode(x) =>
				try {
					(wordNode(x), symbol(x), symbol)
				} catch {
					case _ => (wordNode(x), intType(), symbol)
				}

			case integerNode(x) => (integerNode(x), intType(), symbol)

			case boolNode(x) => (boolNode(x), boolType(), symbol)

			case stringNode(x) => (stringNode(x), stringType(), symbol)

			case unopNode(op, exp) => recurse(exp, typ, symbol)

			case binopNode(op, l, r) =>
				val (left, ltyp, _) = recurse(l, typ, symbol)
				val (right, rtyp, _) = recurse(r, typ, symbol)

				//first run
				var ret = (op, left, right) match {
					case ("/", functionNode(args,body,_), arrayNode(_)) =>
						val tmpName = Util.genRandomName()
						val f = functionNode(args,body,metaNode(tmpName,null))
						//val map = mapNode(f,right)
						val binop = binopNode(op, wordNode(tmpName), right)
						sequenceNode(List(f,binop))
					case (_, _, _) => binopNode(op, left, right)
				}
				(ret, typ, symbol)


			case assignNode(wordNode(id), b) =>
				//TODO: if functionNode and arg has function type in scope. convert from wordNode to call node and capture
				//remaining args
				val (body, btyp, sym) = recurse(b, typ, symbol++ HashMap(id -> voidType()))

				val newsym = symbol ++ HashMap(id -> btyp)

				//TODO: prettify (usage: println=_NS_println)
				val newbody = body match {
					case functionNode(args, fbody,metaData) => functionNode(args,fbody,metaNode(id,null))
					case _ => body
				}

				symbol.contains(id) match {
					case true => (reassignNode(wordNode(id), newbody), typ, newsym)
					case false => (assignNode(wordNode(id), newbody), typ, newsym)
				}


			case blockNode(children) =>
				//TODO register arg variables x;y;z;
				//TODO convert to function type

				//TODO: improve + construct empty node possibly
				if (children.isEmpty) return (functionNode(List(), blockNode(List(returnNode(stringNode("")))), null), functionType(null), symbol)

				val (_, unusedVariables) = findUnusedVariables(blockNode(children), symbol.keySet.to(HashSet))

				var culSymTable = symbol.to(collection.mutable.HashMap)

				val recursedChildren = children.map(x => {
					val (exp, _, localSym) = recurse(x, typ, culSymTable.to(HashMap))
					culSymTable ++= localSym
					exp
				})
				val nChildren = recursedChildren.flatMap {
					case sequenceNode(l) => l
					case x => List(x)
				}
				val childrenWithReturn = nChildren.last match {
					case assignNode(_, _) => nChildren ++ List(returnNode(integerNode(0)))
					case reassignNode(_, _) => nChildren ++ List(returnNode(integerNode(0)))
					case _ => nChildren.init :+ returnNode(nChildren.last)
				}
				(functionNode(unusedVariables.toList.map(wordNode), blockNode(childrenWithReturn),null), typ, symbol)

			case callNode(f, args) =>
				//TODO symbol register each arg if contain assign
				val nargs = args.map(x => recurse(x, typ, symbol)._1)
				val (func, ftyp, sym) = recurse(f, typ, symbol)
				(callNode(func, nargs), typ, sym)


			case arrayNode(elements) =>
				var sym = symbol
				var typ: Type = null
				val nelements = elements.map(x => {
					val (elem, t, s) = recurse(x, typ, sym)
					sym = s
					typ = t
					elem
				})
				(arrayNode(nelements), typ, sym)

			case accessNode(arrayId, idx) =>
				//TODO: recurse array
				/*val ty:Type = arrayId match {
					case arrayNode(elements) => recurse(arrayNode(elements), typ, symbol)._2.asInstanceOf[arrayType]
					case wordNode(id) => symbol(id).asInstanceOf[arrayType]
				}*/
				//TODO: recurse idx
				(accessNode(arrayId, idx), arrayType(), symbol)

			case ifNode(cond, body, elseBody,_) =>
				//TODO: impl returns as "#=1+1"
				val id = Util.genRandomName()
				val (newCond, _, _) = recurse(cond, typ, symbol)
				val newbody = body match {
					case blockNode(elem) =>
						val (blockNode(elems), _, _) = recurse(blockNode(elem), typ, symbol)
						blockNode(elems.init :+ reassignNode(wordNode(id), elems.last))

					case exp =>
						val (b, _, _) = recurse(exp, typ, symbol)
						//TODO: block?
						blockNode(List(reassignNode(wordNode(id), b)))
				}
				val nels = elseBody match {
					case None => None
					case Some(x) => recurse(x, typ, symbol)._1 match {
						case blockNode(elem) =>
							val (blockNode(elems), _, _) = recurse(blockNode(elem), typ, symbol)
							Some(blockNode(elems.init :+ reassignNode(wordNode(id), elems.last)))
						case exp =>
							val (b, _, _) = recurse(exp, typ, symbol)
							Some(blockNode(List(reassignNode(wordNode(id), b))))
					}
				}

				val result = wordNode(id)
				val resultInit = assignNode(result, integerNode(0))
				(sequenceNode(List(resultInit, ifNode(newCond, newbody, nels,id), result)), typ, symbol)
				case mapNode(f,array) =>
					(mapNode(recurse(f, unknownType(),symbol)._1,recurse(array,unknownType(),symbol)._1),arrayType(),symbol)

			case typedNode(exp,ty) => recurse(exp,ty,symbol)

			case x => (x, voidType(), symbol)
		}
	}
}
