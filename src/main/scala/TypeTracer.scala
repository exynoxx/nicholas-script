import scala.collection.mutable
import scala.collection.mutable.ListBuffer

case class typedNode(node: Tree, typ: Type) extends Tree

class TypeTracer extends Stage {
	val functionCallArgs = mutable.HashMap[String, ListBuffer[List[Type]]]()
	var graph = mutable.HashMap[String, Type]()
	var currentScopeName = ""

	def lookupType(op: String, left: Type, right: Type): Type = (op, left, right) match {
		case (_, intType(), intType()) => intType()
		case (_, boolType(), boolType()) => boolType()
		case (_, stringType(), stringType()) => stringType()
		case (_, arrayType(a), arrayType(b)) => arrayType(a)
		case ("++", arrayType(a), _) => arrayType(a)
		case ("++", _, arrayType(b)) => arrayType(b)
		case ("<",_,_) => boolType()
		case ("<=",_,_) => boolType()
		case (">",_,_) => boolType()
		case (">=",_,_) => boolType()
		case ("==",_,_) => boolType()
		case ("!=",_,_) => boolType()
		case _ => intType()
	}

	//first pass
	def dfs1(node: Tree): typedNode = node match {
		case integerNode(_) => typedNode(node, intType())
		case stringNode(_) => typedNode(node, stringType())
		case boolNode(_) => typedNode(node, boolType())
		case wordNode(x) => typedNode(node, graph.getOrElse(x, voidType()))
		case arrayNode(elements) =>
			val typedElements = elements.map(dfs1)
			typedNode(arrayNode(typedElements), arrayType(typedElements.head.typ))
		case unopNode(op, exp) => op match {
			case "!" => typedNode(unopNode(op,dfs1(exp)), boolType())
			case "?" => typedNode(unopNode(op,dfs1(exp)), intType())
		}
		case binopNode(op, l, r) =>
			val ll = dfs1(l)
			val rr = dfs1(r)
			typedNode(binopNode(op, ll, rr), lookupType(op, ll.typ, rr.typ))
		case functionNode(_, _, _, _) =>
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
		case ifNode(c, b, None, id) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, None,id), body.typ)
		case ifNode(c, b, Some(e), id) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, Some(dfs1(e)),id), body.typ)
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
			typedNode(accessNode(typedArray, idx), typedArray.typ.asInstanceOf[arrayType].elementType)
		case comprehensionNode(body, variable, wordNode(array), filter) =>
			val newBody = dfs1(body)
			val newFilter = filter match {
				case Some(f) => Some(dfs1(f))
				case None => None
			}
			val listType = graph.getOrElse(array, unknownType()).asInstanceOf[arrayType]
			val newVariable = typedNode(variable, listType.elementType)
			typedNode(comprehensionNode(newBody, newVariable, wordNode(array), newFilter), listType)
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
		case typedNode(_, typ) => typ
		case arrayNode(elements) => findTypeOfReturn(elements.head)
		case mapNode(_, array) => findTypeOfReturn(array)
		case x => throw new Exception("Could not find findTypeOfReturn: " + node.toString)
	}

	def recurseTypedTree(node: Tree): typedNode = node match {
		//TODO: type captured vars using scope
		case typedNode(functionNode(captured, args, body, metaNode(name, _)), _) =>
			//paste back in
			val elementOption = functionCallArgs.get(name)
			elementOption match {
				case Some(listOfListOfArgs: ListBuffer[List[Type]]) =>
					//TODO spawn copies

					//captured args
					val typedCaptures = captured.map{case wordNode(id) => typedNode(wordNode(id),graph(id))}

					//args
					val argTypes = listOfListOfArgs.head.drop(captured.length) //only type based on args not captured
					val typedArgs = args.zip(argTypes).map { case (node, typ) => typedNode(node, typ) }

					//body
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

					//finish
					typedNode(functionNode(typedCaptures,typedArgs, fbody, metaNode(name, null)), fbodyType)
				//TODO: dont paste null
				case None => typedNode(nullLeaf(), unknownType())
			}
		case typedNode(assignNode(wordNode(id), body), _) =>
			val b = recurseTypedTree(body)
			if (b.node.isInstanceOf[nullLeaf]) return typedNode(nullLeaf(), unknownType())
			typedNode(assignNode(wordNode(id), b), b.typ)
		case typedNode(reassignNode(wordNode(id), body), _) =>
			val b = recurseTypedTree(body)
			if (b.node.isInstanceOf[nullLeaf]) return typedNode(nullLeaf(), unknownType())
			typedNode(reassignNode(wordNode(id), b), b.typ)
		case typedNode(callNode(wordNode(id), args), ty) =>
			if (ty != unknownType()) return typedNode(callNode(wordNode(id), args), ty)
			val typ = graph.getOrElse(id, unknownType())
			typedNode(callNode(typedNode(wordNode(id),typ), args.map(recurseTypedTree)), typ)
		case typedNode(wordNode(x), ty) =>
			typedNode(wordNode(x), graph.getOrElse(x, voidType()))
		case typedNode(ifNode(c, b, None, id), _) =>
			val typedBody = recurseTypedTree(b)
			typedNode(ifNode(recurseTypedTree(c), typedBody, None,id), typedBody.typ)
		case typedNode(ifNode(c, b, Some(e), id), _) =>
			val typedBody = recurseTypedTree(b)
			typedNode(ifNode(recurseTypedTree(c), typedBody, Some(recurseTypedTree(e)),id), typedBody.typ)
		case typedNode(exp, typ) =>
			val content = exp match {
				case arrayNode(elements) => arrayNode(elements.map(recurseTypedTree))
				case comprehensionNode(a, b, c, d) => comprehensionNode(a, b, c, d) //TODO: recurse again. in case if funcall
				case binopNode(op, l, r) => binopNode(op, recurseTypedTree(l), recurseTypedTree(r))
				case unopNode(op, exp) => unopNode(op, recurseTypedTree(exp))
				case returnNode(exp) => returnNode(recurseTypedTree(exp))
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
		functionNode(List(),main.args, typedBlock, main.metaData)
	}
}





/*
* x= 5;
* print x
* y="ss"
* x=y
* */