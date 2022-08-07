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
			val typedNode(left, lty) = recurse(l)
			val typedNode(right, rty) = recurse(r)

			(op, lty, rty) match {
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
					val f = typedNode(functionNode(List(), List(typedNode(wordNode(elementId), ty)), fbody, metaNode(Util.genRandomName(), null)), functionType(null))
					mapNode(f, typedNode(right, rty))

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
					val f = typedNode(functionNode(List(), List(typedNode(wordNode(elementId), ty)), fbody, metaNode(Util.genRandomName(), null)), functionType(null))
					mapNode(f, typedNode(left, lty))
				case (_, _, _) => binopNode(op, typedNode(left, lty), typedNode(right, rty))
			}

		case assignNode(id, body) => assignNode(id, recurse(body))
		case reassignNode(id, body) => reassignNode(id, recurse(body))
		case blockNode(children) => blockNode(children.map(recurse))
		case callNode(f, args) =>
			val newF = recurse(f)
			val newArgs = args.map(recurse)
			newF match {
				case typedNode(wordNode(_), arrayType(ty)) => typedNode(mapNode(newArgs.head, newF), arrayType(ty))
				case _ => callNode(newF, newArgs)
			}
		case arrayNode(elements) => arrayNode(elements.map(recurse))
		case accessNode(arrayId, idx) => accessNode(arrayId, recurse(idx))
		case ifNode(cond, body, None, meta) => ifNode(recurse(cond), recurse(body), None, meta)
		case ifNode(cond, body, Some(elseBody), meta) => ifNode(recurse(cond), recurse(body), Some(recurse(elseBody)), meta)
		case mapNode(f, array) => mapNode(recurse(f), recurse(array))
		case typedNode(exp, ty) => typedNode(recurse(exp), ty)
		case functionNode(captured, args, body, meta) => functionNode(captured, args, recurse(body), meta)
		case returnNode(exp) => returnNode(recurse(exp))
		case comprehensionNode(body, variable, array, filter) => comprehensionNode(recurse(body), variable, array, filter)
		case lambdaNode(cap, args, body) => lambdaNode(cap, args, recurse(body))
		case nullLeaf() => nullLeaf()
		case x => throw new NotImplementedError(x.toString)
	}
}
