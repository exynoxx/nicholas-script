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
				case wordNode(oldId) =>
					var id = oldId
					while (replaceMap.contains(id)) id = replaceMap(id)
					wordNode(id)
				case stringNode(x) => stringNode(x)
				case boolNode(x) => boolNode(x)
				case integerNode(x) => integerNode(x)

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
				//TODO fix anon f,
				case callNode(wordNode(id), args) => callNode(wordNode(replaceMap.getOrElse(id, id)), args.map(inlineTypes))
				case callNode(f, args) => callNode(f, args.map(inlineTypes))
				case blockNode(children) => blockNode(children.map(inlineTypes))
				case arrayNode(l) => arrayNode(l.map(inlineTypes))
				case castNode(exp,from,to) => castNode(inlineTypes(exp),from,to)
				case accessNode(exp,index) => accessNode(inlineTypes(exp),inlineTypes(index))

				//TODO: fix these
				case lambdaNode(captured,args,body) => lambdaNode(captured,args,body)
				case mapNode(f,array) => mapNode(f,array)
				case functionNode(cap,args,body,meta) => functionNode(cap,args,body,meta)
				case lambdaNode(cap,args,body) => lambdaNode(cap,args,body)
				//case typedNode(exp,ty) => typedNode(inlineTypes(exp),ty)
				case _ => throw new Exception("node not handled: " + typNode.toString)
			}
			typedNode(treeNode, ty)
		case _ => throw new Exception("not typed node "+node.toString)
	}

	def process(main: Tree): Tree = {
		println("---------------------- inlining ----------------------")
		val function = main.asInstanceOf[functionNode]
		val body = function.body.asInstanceOf[blockNode]
		val inlined = body.children.map(inlineTypes)
		functionNode(List(),function.args, blockNode(inlined), function.metaData)
	}


}
