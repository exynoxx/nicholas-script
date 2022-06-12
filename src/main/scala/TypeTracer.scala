import scala.collection.mutable
import scala.collection.mutable.ListBuffer

case class typedNode(node: Tree, typ: Type) extends Tree

class TypeTracer extends Stage {
	val functionCallArgs = mutable.HashMap[String, ListBuffer[List[Type]]]()
	var graph = mutable.HashMap[String, Type]()
	var currentScopeName = ""

	def lookupType(left: Type, right: Type): Type = (left, right) match {
		case (intType(), intType()) => intType()
		case (boolType(), boolType()) => boolType()
		case (stringType(), stringType()) => stringType()
		case (arrayType(a), arrayType(b)) => arrayType(a)
		case _ => intType()
	}

	//first pass
	def dfs1(node: Tree): typedNode = node match {
		case integerNode(_) => typedNode(node, intType())
		case stringNode(_) => typedNode(node, stringType())
		case wordNode(x) => typedNode(node, graph.getOrElse(x, voidType()))
		case arrayNode(elements) =>
			val typedElements = elements.map(dfs1)
			typedNode(arrayNode(typedElements), arrayType(typedElements.head.typ))
		case binopNode(op, l, r) =>
			val ll = dfs1(l)
			val rr = dfs1(r)
			typedNode(binopNode(op, ll, rr), lookupType(ll.typ, rr.typ))
		case functionNode(_, _, _) =>
			typedNode(node, unknownType())
		case assignNode(wordNode(id), body) =>
			val recurs = dfs1(body)
			graph.addOne(id -> recurs.typ)
			typedNode(assignNode(wordNode(id), recurs), recurs.typ)
		//TODO: fix duplicate
		case reassignNode(wordNode(id), body) =>
			val recurs = dfs1(body)
			graph.addOne(id -> recurs.typ)
			typedNode(reassignNode(wordNode(id), recurs), recurs.typ)
		case callNode(wordNode(id), args) =>
			val argTypes = args.map(dfs1)
			functionCallArgs.getOrElseUpdate(id, new ListBuffer()).addOne(argTypes.map(_.typ))
			typedNode(callNode(wordNode(id), argTypes), unknownType())
		case ifNode(c, b, None, _) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, None), body.typ)
		case ifNode(c, b, Some(e), _) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, Some(dfs1(e))), body.typ)
		case returnNode(exp) =>
			val body = dfs1(exp)
			typedNode(returnNode(body), body.typ)
		case blockNode(children) =>
			val graphCopy = graph.clone()
			val body = doScope(node.asInstanceOf[blockNode])
			val ty = findTypeOfReturn(body)
			graph = graphCopy
			typedNode(body, ty)
		case mapNode(wordNode(f), array) =>
			val typedArray = dfs1(array)
			functionCallArgs.getOrElseUpdate(f, new ListBuffer()).addOne(List(typedArray.typ.asInstanceOf[arrayType].elementType))
			typedNode(mapNode(dfs1(wordNode(f)), typedArray), unknownType())
		case accessNode(arr, idx) =>
			val typedArray = dfs1(arr)
			typedNode(accessNode(typedArray, idx), typedArray.typ)
		case typedNode(exp, ty) =>
			typedNode(exp, ty)
		case x => throw new NotImplementedError(x.toString)
	}

	def findTypeOfReturn(node: Tree): Type = node match {
		case blockNode(children) =>
			val c = children.map(findTypeOfReturn)
			val candidates = c.filterNot(_ == voidType() || c == unknownType())
			if (candidates.isEmpty) return unknownType()
			candidates.last
		case ifNode(_, b, None, _) => findTypeOfReturn(b)
		case ifNode(_, b, Some(e), _) =>
			findTypeOfReturn(b) match {
				case voidType() | unknownType() => findTypeOfReturn(e)
				case x => x
			}
		case assignNode(_, body) => findTypeOfReturn(body)
		case typedNode(returnNode(_), typ) => typ
		case arrayNode(elements) => findTypeOfReturn(elements.head)
		case mapNode(_, array) => findTypeOfReturn(array)
		case x => voidType()
	}

	def recurseTypedTree(node: Tree): typedNode = node match {
		case typedNode(functionNode(args, body, metaNode(name, _)), _) =>
			//paste back in
			val elementOption = functionCallArgs.get(name)
			elementOption match {
				case Some(listOfListOfArgs: ListBuffer[List[Type]]) =>
					//TODO spawn copies
					val argTypes = listOfListOfArgs(0)
					val typedArgs = args.zip(argTypes).map { case (node, typ) => typedNode(node, typ) }
					//TODO send recursively

					val graphCopy = graph.clone()
					val oldScope = currentScopeName
					currentScopeName = name
					typedArgs.foreach { case typedNode(wordNode(id), ty) => graph.addOne(id -> ty) }
					val fbody = doScope(body.asInstanceOf[blockNode])
					val fbodyType = findTypeOfReturn(fbody)
					graph = graphCopy
					currentScopeName = oldScope
					graph.addOne(name -> fbodyType)
					typedNode(functionNode(typedArgs, fbody, metaNode(name, null)), fbodyType)
				//TODO: dont paste null
				case None => typedNode(functionNode(args, body, metaNode(name, null)), null)
			}
		case typedNode(assignNode(wordNode(id), body), _) =>
			val b = recurseTypedTree(body)
			typedNode(assignNode(wordNode(id), b), b.typ)
		case typedNode(reassignNode(wordNode(id), body), _) =>
			val b = recurseTypedTree(body)
			typedNode(reassignNode(wordNode(id), b), b.typ)

		case typedNode(callNode(wordNode(id), args), ty) =>
			if (ty != unknownType()) return typedNode(callNode(wordNode(id), args), ty)
			typedNode(callNode(wordNode(id), args.map(recurseTypedTree)), graph.getOrElse(id, unknownType()))
		case typedNode(wordNode(x), ty) =>
			typedNode(wordNode(x), graph.getOrElse(x, voidType()))

		case typedNode(exp, typ) =>
			val content = exp match {
				case arrayNode(elements) => arrayNode(elements.map(recurseTypedTree))
				case binopNode(op, l, r) =>
					binopNode(op, recurseTypedTree(l), recurseTypedTree(r))
				case ifNode(c, b, None, _) =>
					ifNode(recurseTypedTree(c), recurseTypedTree(b), None)
				case ifNode(c, b, Some(e), _) =>
					ifNode(recurseTypedTree(c), recurseTypedTree(b), Some(recurseTypedTree(e)))
				case returnNode(exp) =>
					returnNode(recurseTypedTree(exp))
				case x => x
			}
			typedNode(content, typ)
	}

	//TODO: more than 2 runs
	def doScope(scope: blockNode): Tree = {
		val firstRun = scope.children.map(dfs1)
		blockNode(firstRun.map(recurseTypedTree))
	}

	def injectExternalMethods() = {
		graph.addOne("println" -> intType())
		graph.addOne("print" -> intType())
	}

	def process(tree: Tree): Tree = {
		println("-------------------- type tracing --------------------")
		val main = tree.asInstanceOf[functionNode]
		injectExternalMethods()
		val body = main.body.asInstanceOf[blockNode]
		val typedBlock = doScope(body)
		functionNode(main.args, typedBlock, main.metaData)
	}
}





/*
* x= 5;
* print x
* y="ss"
* x=y
* */