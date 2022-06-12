class TypeAugmenter {

	def process(AST: Tree): Tree = {
		println("------------------- type augmenting ------------------")
		recurse(AST)
	}

	def recurse(AST: Tree): Tree = {
		AST match {
			case wordNode(x) =>wordNode(x)
			case integerNode(x) => integerNode(x)
			case stringNode(x) => stringNode(x)
			case unopNode(op, exp) => unopNode(op,recurse(exp))
			case binopNode(op, l, r) =>
				val typedNode(left,lty) = recurse(l)
				val typedNode(right,rty) = recurse(r)

				(op, lty,rty) match {
					//case (intType(),intType())
					case ("*", arrayType(_),_) => callNode(wordNode("_NS_repeat"),List(typedNode(left,lty),typedNode(right,rty)))
					case ("*", _,arrayType(_)) => callNode(wordNode("_NS_repeat"),List(typedNode(right,rty), typedNode(left,lty)))
					case (_, _,arrayType(ty)) =>
						val elementId = Util.genRandomName()
						val arrayElement = typedNode(wordNode(elementId),ty)
						val fbody = blockNode(List(
							typedNode(
								returnNode(
									typedNode(
										binopNode(
											op,
											typedNode(left,lty),
											arrayElement
										),
										rty)
								),
								rty)
						))
						val f = functionNode(List(typedNode(wordNode(elementId),ty)),fbody,metaNode(Util.genRandomName(),null))
						mapNode(f,typedNode(right,rty))

					//TODO: make right case
					//case (_,arrayType(ty),_) =>
					case (_,_,_) => binopNode(op, typedNode(left,lty),typedNode(right,rty))
				}

			case assignNode(id, body) => assignNode(id,recurse(body))
			case reassignNode(id, body) => reassignNode(id,recurse(body))
			case blockNode(children) => blockNode(children.map(recurse))
			case callNode(f, args) => callNode(recurse(f), args.map(recurse))
			case arrayNode(elements) => arrayNode(elements.map(recurse))
			case accessNode(arrayId, idx) => accessNode(arrayId, recurse(idx))
			case ifNode(cond, body, None, meta) => ifNode(recurse(cond),recurse(body),None,meta)
			case ifNode(cond, body, Some(elseBody), meta) => ifNode(recurse(cond),recurse(body), Some(recurse(elseBody)),meta)
			case mapNode(f, array) => mapNode(recurse(f), recurse(array))
			case typedNode(exp, ty) => typedNode(recurse(exp),ty)
			case functionNode(args, body,meta) => functionNode(args,recurse(body),meta)
			case returnNode(exp) => returnNode(recurse(exp))
			case x => throw new NotImplementedError(x.toString)
		}
	}
}
