import scala.collection.mutable

class TypeInliner {

	//### responsibility ###
	//1) clone
	//2) extract and replace func names

	val symbolMap = mutable.HashMap[String, Type]()
	val replaceMap = mutable.HashMap[String, String]()


	def inlineTypes(node: Tree): Tree = node match {
		case typedNode(typNode, ty) =>
			val treeNode = typNode match {
				case assignNode(wordNode(id), typedNode(body, typ)) =>
					symbolMap.addOne(id -> typ)
					assignNode(wordNode(id), typedNode(inlineTypes(body), typ))
				case reassignNode(wordNode(oldId), typedNode(body, typ)) =>
					//replace id from previous type change
					var id = oldId
					var lastId = oldId
					while (symbolMap(id) != typ && replaceMap.contains(id)) {
						lastId = id
						id = replaceMap(id)
					}

					//new tyoe change
					if (symbolMap(id) != typ) {
						id = Util.genRandomName()
						symbolMap.addOne(id -> typ)
						replaceMap.addOne(lastId -> id)
					}
					reassignNode(wordNode(id), typedNode(inlineTypes(body), typ))
				case ifNode(c, body, None) =>
					ifNode(c, inlineTypes(body), None)
				case ifNode(c, body, Some(e)) =>
					ifNode(c, inlineTypes(body), Some(inlineTypes(e)))
			}
			typedNode(treeNode, ty)
		case _ => node
	}

	def process(main: functionNode): Tree = {
		val body = main.body.asInstanceOf[blockNode]
		val inlined = body.children.map(inlineTypes)
		functionNode(main.name, main.args, blockNode(inlined))
	}


}
