import scala.collection.mutable.ListBuffer

class TypeAugmenter extends Stage {

	def process(AST: Tree): Tree = {
		println("------------------- type augmenting ------------------")
		recurse(AST)
	}

	def recurse(AST: Tree): Tree = AST match {
		case wordNode(x) => wordNode(x)
		case integerNode(x) => integerNode(x)
		case boolNode(x) => boolNode(x)
		case stringNode(x) => stringNode(x)
		case unopNode(op, exp) => op match {
			case "?" => exp match {
				case typedNode(_, arrayType(_)) => typedNode(callNode(wordNode("_NS_len"), List(exp)), intType())
				//contains functionality for MAP here
			}
			case _ => unopNode(op, recurse(exp))
		}
		case binopNode(op, l, r) =>

			val extractedNodes = ListBuffer[Tree]()
			val (left, lty) = recurse(l) match {
				case typedNode(sequenceNode(ll), llty) =>
					extractedNodes ++= ll.init
					(ll.last, llty)
				case typedNode(ll, llty) => (ll, llty)
			}
			val (right, rty) = recurse(r) match {
				case typedNode(sequenceNode(rr), rrty) =>
					extractedNodes ++= rr.init
					(rr.last, rrty)
				case typedNode(rr, rrty) => (rr, rrty)
			}

			val newBinop = (op, lty, rty) match {
				//case (intType(),intType())
				case ("*", arrayType(_), _) => callNode(wordNode("_NS_repeat"), List(typedNode(left, lty), typedNode(right, rty)))
				case ("*", _, arrayType(_)) => callNode(wordNode("_NS_repeat"), List(typedNode(right, rty), typedNode(left, lty)))
				//TODO: make work on ++ element as well
				case ("++", _, arrayType(_)) => callNode(wordNode("_NS_concat"), List(typedNode(left, lty), typedNode(right, rty)))
				case ("++", arrayType(_), _) => callNode(wordNode("_NS_concat"), List(typedNode(left, lty), typedNode(right, rty)))
				case (_, _, arrayType(ty)) =>
					val elementId = Util.genRandomName()
					val arrayElement = typedNode(wordNode(elementId), ty)
					val fbody = blockNode(List(
						typedNode(
							returnNode(
								typedNode(
									binopNode(
										op,
										typedNode(left, lty),
										arrayElement
									),
									lty)
							),
							lty)
					))
					val f = typedNode(lambdaNode(List(), List(typedNode(wordNode(elementId), ty)), fbody), lambdaType(lty, List(ty)))
					val id = Util.genRandomName()
					val assign = typedNode(assignNode(wordNode(id), f), f.typ)
					extractedNodes += assign
					typedNode(mapNode(wordNode(id), typedNode(right, rty)), arrayType(lty))


				//TODO: make right case
				case (_, arrayType(ty), _) =>
					val elementId = Util.genRandomName()
					val arrayElement = typedNode(wordNode(elementId), ty)
					val fbody = blockNode(List(
						typedNode(
							returnNode(
								typedNode(
									binopNode(
										op,
										arrayElement,
										typedNode(right, rty)
									),
									rty)
							),
							rty)
					))
					val f = typedNode(lambdaNode(List(), List(typedNode(wordNode(elementId), ty)), fbody), lambdaType(rty, List(ty)))
					val id = Util.genRandomName()
					val assign = typedNode(assignNode(wordNode(id), f), f.typ)
					extractedNodes += assign
					typedNode(mapNode(wordNode(id), typedNode(left, lty)), arrayType(rty))

				case ("+", stringType(), intType()) => binopNode(op, typedNode(left, lty), typedNode(castNode(typedNode(right, rty), rty, stringType()), stringType()))
				case ("+", intType(), stringType()) => binopNode(op, typedNode(castNode(typedNode(left, lty), lty, stringType()), stringType()), typedNode(right, rty))
				case ("+", stringType(), boolType()) => binopNode(op, typedNode(left, lty), typedNode(castNode(typedNode(right, rty), rty, stringType()), stringType()))
				case ("+", boolType(), stringType()) => binopNode(op, typedNode(castNode(typedNode(left, lty), lty, stringType()), stringType()), typedNode(right, rty))

				//case ("*", stringType(),intType())
				//case ("*", intType(),stringType())

				case (_, _, _) => binopNode(op, typedNode(left, lty), typedNode(right, rty))
			}
			if (extractedNodes.isEmpty)
				newBinop
			else
				sequenceNode(extractedNodes.toList :+ newBinop)


		case assignNode(id, body) =>
			recurse(body) match {
				case typedNode(sequenceNode(l), ty) => sequenceNode(l.init :+ typedNode(assignNode(id, l.last), ty))
				case x => assignNode(id, x)
			}

		case reassignNode(id, body) =>
			recurse(body) match {
				case typedNode(sequenceNode(l), ty) => sequenceNode(l.init :+ typedNode(reassignNode(id, l.last), ty))
				case x => reassignNode(id, x)
			}

		case blockNode(children) =>
			val flatChildren = children.map(recurse).flatMap {
				case typedNode(sequenceNode(l), _) => l
				case x => List(x)
			}
			blockNode(flatChildren)
		case callNode(f, args) =>
			val recursedFunction = recurse(f)
			val extractedNodes = ListBuffer[Tree]()
			val newArgs = args.map(recurse).map {
				case typedNode(sequenceNode(list), ty) =>
					extractedNodes ++= list.init
					typedNode(list.last, ty)
				case x => x
			}


			val callnode = recursedFunction match {
				//mapping array, this is not call
				case typedNode(wordNode(_), arrayType(ty)) =>
					newArgs.head match {
						//func is anonymous, extract first
						case typedNode(functionNode(cap,args,body,_),functionType(ty)) =>

							val lambda = typedNode(lambdaNode(cap,args,body),lambdaType(ty,args.map{case typedNode(_,tt)=>tt}))
							val (extract,replacement) = Util.extractTypedNode(lambda)
							extractedNodes+=extract
							typedNode(mapNode(replacement, recursedFunction),arrayType(ty))
						//default
						case func => mapNode(func, recursedFunction)
					}
				//default
				case _ => callNode(recursedFunction, newArgs)
			}

			if (extractedNodes.isEmpty)
				callnode
			else
				sequenceNode(extractedNodes.toList :+ callnode)

		case arrayNode(elements) => arrayNode(elements.map(recurse))
		case accessNode(arrayId, idx) => accessNode(arrayId, recurse(idx))
		case ifNode(cond, body, None, meta) => ifNode(recurse(cond), recurse(body), None, meta)
		case ifNode(cond, body, Some(elseBody), meta) => ifNode(recurse(cond), recurse(body), Some(recurse(elseBody)), meta)
		case mapNode(f, array) => mapNode(recurse(f), recurse(array))
		case typedNode(exp, ty) => typedNode(recurse(exp), ty)
		case functionNode(captured, args, body, meta) => functionNode(captured, args, recurse(body), meta)
		case returnNode(exp) => returnNode(recurse(exp))
		case comprehensionNode(body, variable, array, filter) =>
			val (lambdaBody, newBody) = Util.extractTypedNode(recurse(body))
			filter match {
				case Some(filter) =>
					val (lambdaFilter, newFilter) = Util.extractTypedNode(filter)
					val result = comprehensionNode(newBody, variable, array, Some(newFilter))
					sequenceNode(List(lambdaBody, lambdaFilter, result))
				case None =>
					val result = comprehensionNode(newBody, variable, array, None)
					sequenceNode(List(lambdaBody, result))
			}

		case lambdaNode(cap, args, body) => lambdaNode(cap, args, recurse(body))
		case nullLeaf() => nullLeaf()
		case x => throw new NotImplementedError(x.toString)
	}
}
