import scala.collection.mutable

class TypeInliner {

	//### responsibility ###
	//1) clone
	//2) extract and replace func names
	//TODO: remove void-returning (return(call(voidF))

	val symbolMap = mutable.HashMap[String, Type]()
	val replaceMap = mutable.HashMap[String, String]()


	def inlineTypes(node: Tree): Tree = node match {
		case typedNode(typNode, ty) =>
			val treeNode = typNode match {
				case assignNode(wordNode(id), typedNode(body, typ)) =>
					symbolMap.addOne(id -> typ)
					assignNode(wordNode(id), inlineTypes(typedNode(body, typ)))
				case reassignNode(wordNode(oldId), typedNode(body, typ)) =>
					//replace id from previous type change
					var id = oldId
					var lastId = oldId
					while (symbolMap(id) != typ && replaceMap.contains(id)) {
						lastId = id
						id = replaceMap(id)
					}

					//new tyoe change
					if (symbolMap(id) == typ) {
						reassignNode(wordNode(id), inlineTypes(typedNode(body, typ)))
					} else {
						id = Util.genRandomName()
						symbolMap.addOne(id -> typ)
						replaceMap.addOne(lastId -> id)
						assignNode(wordNode(id), inlineTypes(typedNode(body, typ)))
					}

				case ifNode(c, body, None,_) =>
					ifNode(c, inlineTypes(body), None)
				case ifNode(c, body, Some(e),_) =>
					ifNode(c, inlineTypes(body), Some(inlineTypes(e)))
				case binopNode(op, l, r) =>
					binopNode(op, inlineTypes(l), inlineTypes(r))
				case returnNode(exp) =>
					returnNode(inlineTypes(exp))
				case wordNode(oldId) =>
					var id = oldId
					while (replaceMap.contains(id)) id = replaceMap(id)
					wordNode(id)
				//TODO fix anon f,
				case callNode(f, args) => callNode(f, args.map(inlineTypes))

				case x => x
			}
			typedNode(treeNode, ty)
		case _ => node
	}

	def process(main: Tree): Tree = {
		val function = main.asInstanceOf[functionNode]
		val body = function.body.asInstanceOf[blockNode]
		val inlined = body.children.map(inlineTypes)
		functionNode(function.name, function.args, blockNode(inlined))
	}


}
