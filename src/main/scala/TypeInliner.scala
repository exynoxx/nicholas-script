import scala.collection.mutable

class TypeInliner extends Stage{

	//### responsibility ###
	//1) clone
	//2) extract and replace func names
	//TODO: remove void-returning (return(call(voidF))

	val symbolMap = mutable.HashMap[String, Type]()
	val replaceMap = mutable.HashMap[String, String]("print"->"_NS_print","println"->"_NS_println")


	def inlineTypes(node: Tree): Tree = node match {
		case typedNode(typNode, ty) =>
			val treeNode: Tree = typNode match {
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

				case ifNode(c, body, None, meta) =>
					ifNode(c, inlineTypes(body), None,meta)
				case ifNode(c, body, Some(e), meta) =>
					ifNode(c, inlineTypes(body), Some(inlineTypes(e)),meta)
				case binopNode(op, l, r) =>
					binopNode(op, inlineTypes(l), inlineTypes(r))
				case returnNode(exp) =>
					returnNode(inlineTypes(exp))
				case wordNode(oldId) =>
					var id = oldId
					while (replaceMap.contains(id)) id = replaceMap(id)
					wordNode(id)
				//TODO fix anon f,
				case callNode(wordNode(id), args) => callNode(wordNode(replaceMap.getOrElse(id, id)), args.map(inlineTypes))
				case callNode(f, args) => callNode(f, args.map(inlineTypes))
				/*case functionNode(args, blockNode(children), meta) =>
					val metaNode(name, _) = meta
					val extractName = Util.genRandomName()
					replaceMap.addOne(name -> extractName)
					val ret = functionNode(args, blockNode(children.map(inlineTypes)), metaNode(name, extractName))
					replaceMap.remove(name)
					ret*/
				case blockNode(children) => blockNode(children.map(inlineTypes))
				case x => x
			}
			typedNode(treeNode, ty)
		case _ => node
	}

	def process(main: Tree): Tree = {
		println("---------------------- inlining ----------------------")
		val function = main.asInstanceOf[functionNode]
		val body = function.body.asInstanceOf[blockNode]
		val inlined = body.children.map(inlineTypes)
		functionNode(function.args, blockNode(inlined), function.metaData)
	}


}
