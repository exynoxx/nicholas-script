import scala.collection.mutable
import scala.collection.mutable.ListBuffer

case class typedNode(node: Tree, typ: Type) extends Tree

class TypeTracer extends Stage {
	val functionCallArgs = mutable.HashMap[String, ListBuffer[List[Type]]]()
	var graph = mutable.HashMap[String, Type]()
	var currentScopeName = ""

	def lookupType(op: String, left: Type, right: Type): Type = (op, left, right) match {
		case ("++", arrayType(a), _) => arrayType(a)
		case ("++", _, arrayType(b)) => arrayType(b)
		case ("<", _, _) => boolType()
		case ("<=", _, _) => boolType()
		case (">", _, _) => boolType()
		case (">=", _, _) => boolType()
		case ("==", _, _) => boolType()
		case ("!=", _, _) => boolType()
		case ("*", intType(), arrayType(ty)) => arrayType(ty)
		case ("*", boolType(), arrayType(ty)) => arrayType(ty)
		case ("*", arrayType(ty), intType()) => arrayType(ty)
		case ("*", arrayType(ty), boolType()) => arrayType(ty)
		case ("+", arrayType(ty), rty) => arrayType(lookupType(op, ty, rty))
		case ("+", lty, arrayType(ty)) => arrayType(lookupType(op, lty, ty))
		case ("+", stringType(), _) => stringType()
		case ("+", _, stringType()) => stringType()
		case (_, intType(), intType()) => intType()
		case (_, boolType(), boolType()) => boolType()
		case (_, stringType(), stringType()) => stringType()
		case (_, arrayType(a), arrayType(b)) => arrayType(a)
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
			case "!" => typedNode(unopNode(op, dfs1(exp)), boolType())
			case "?" => typedNode(unopNode(op, dfs1(exp)), intType())
		}
		case binopNode(op, l, r) =>
			val ll = dfs1(l)
			val rr = dfs1(r)
			typedNode(binopNode(op, ll, rr), lookupType(op, ll.typ, rr.typ))
		case functionNode(_, _, _, _) =>
			typedNode(node, functionType(unknownType()))
		case lambdaNode(_, _, _) =>
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
			typedNode(ifNode(dfs1(c), body, None, id), body.typ)
		case ifNode(c, b, Some(e), id) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, Some(dfs1(e)), id), body.typ)
		case returnNode(exp) =>
			val body = dfs1(exp)
			typedNode(returnNode(body), body.typ)
		case blockNode(children) =>
			val graphCopy = graph.clone()
			val body = doScope(node.asInstanceOf[blockNode])
			val ty = findTypeOfReturn(body)
			graph = graphCopy
			typedNode(body, ty)
		case mapNode(f, array) =>
			val typedArray = dfs1(array)
			val typedF = dfs1(f) match {
				case typedNode(wordNode(fname), typ) =>
					functionCallArgs.getOrElseUpdate(fname, new ListBuffer()).addOne(List(typedArray.typ.asInstanceOf[arrayType].elementType))
					typedNode(wordNode(fname), typ)
				case typedNode(lambdaNode(cap, args, body), _) =>
					typedNode(lambdaNode(cap, args, body), typedArray.typ)
				case x => x
			}
			typedNode(mapNode(typedF, typedArray), typedArray.typ)
		case accessNode(arr, idx) =>
			val typedArray = dfs1(arr)
			val typedIdx = dfs1(idx)
			typedNode(accessNode(typedArray, typedIdx), typedArray.typ.asInstanceOf[arrayType].elementType)

		case comprehensionNode(body, variable, array, filter) =>
			//TODO: support function call
			val newArray = dfs1(array)
			val arrayType = newArray.typ.asInstanceOf[arrayType]

			val newBody = dfs1(body) match {
				case typedNode(lambdaNode(cap, args, body), _) =>
					typedNode(lambdaNode(cap, args, body), arrayType)
				case x => x
			}
			val newFilter = filter match {
				case Some(f) =>
					val newF = dfs1(f) match {
						case typedNode(lambdaNode(cap, args, body), _) =>
							typedNode(lambdaNode(cap, args, body), arrayType)
						case x => x
					}

					Some(newF)
				case None => None
			}
			val newVariable = typedNode(variable, arrayType.elementType)
			typedNode(comprehensionNode(newBody, newVariable, newArray, newFilter), arrayType)

		case rangeNode(from,to) => typedNode(rangeNode(dfs1(from),dfs1(to)),arrayType(intType()))

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

	def recurseFunction(f: functionNode, listOfListOfArgs: ListBuffer[List[Type]]): typedNode = {
		//TODO spawn copies

		//captured args
		val typedCaptures = f.captured
			.map { case wordNode(id) => typedNode(wordNode(id), graph(id)) }
			.filter{case typedNode(_,typ) => !typ.isInstanceOf[functionType]}

		//args
		val argTypes = listOfListOfArgs.head.drop(typedCaptures.length) //only type based on args not captured

		val typedArgs = f.args.zip(argTypes).map { case (node, typ) => typedNode(node, typ) }

		//body
		//TODO send recursively
		val graphCopy = graph.clone()
		val oldScope = currentScopeName
		currentScopeName = f.metaData.name
		typedArgs.foreach { case typedNode(wordNode(id), ty) => graph.addOne(id -> ty) }
		val fbody = doScope(f.body.asInstanceOf[blockNode])
		val fbodyType = findTypeOfReturn(fbody)
		graph = graphCopy
		currentScopeName = oldScope
		graph.addOne(f.metaData.name -> functionType(fbodyType))

		//finish
		typedNode(functionNode(typedCaptures, typedArgs, fbody, f.metaData), functionType(fbodyType))
	}

	def recurseTypedTree(node: Tree): typedNode = node match {
		//TODO: type captured vars using scope
		case typedNode(functionNode(captured, args, body, metaData), _) =>
			//paste back in
			functionCallArgs.get(metaData.name) match {

				case Some(listOfListOfArgs: ListBuffer[List[Type]]) =>
					recurseFunction(functionNode(captured, args, body, metaData), listOfListOfArgs)

				case None =>
					typedNode(nullLeaf(), unknownType())
			}

		case typedNode(lambdaNode(captured, args, body), ty) =>
			//getOrElse is a temporary fix
			val typedCaptures = captured.map { case wordNode(id) => typedNode(wordNode(id), graph.getOrElse(id, unknownType())) }

			//args
			//here ty comes from mapNode or comprehension. Has to be an array type
			val typedArgs = args.length match {
				case 0 => List()
				case _ => ty match {
					case arrayType(elementType) => List(typedNode(args.head, elementType))
					case _ => throw new IllegalArgumentException(node.toString)
				}

			}

			//body
			val graphCopy = graph.clone()
			typedArgs.foreach { case typedNode(wordNode(id), ty) => graph.addOne(id -> ty) }
			val fbody = doScope(body.asInstanceOf[blockNode])
			val fbodyType = findTypeOfReturn(fbody)
			graph = graphCopy

			//finish
			typedNode(lambdaNode(typedCaptures, typedArgs, fbody), lambdaType(fbodyType, typedArgs.map { case typedNode(_, ty) => ty }))

		case typedNode(assignNode(wordNode(id), body), ty) =>
			val b = recurseTypedTree(body)
			graph.addOne(id -> b.typ)
			typedNode(assignNode(wordNode(id), b), b.typ)
		case typedNode(reassignNode(wordNode(id), body), ty) =>
			val b = recurseTypedTree(body)
			graph.addOne(id -> b.typ)
			typedNode(reassignNode(wordNode(id), b), b.typ)

		case typedNode(callNode(wordNode(id), args), ty) =>
			if (ty != unknownType()) return typedNode(callNode(wordNode(id), args), ty)
			var typ = graph.getOrElse(id, unknownType())

			//HACK! FIX TODO LATER...
			//if this is an array being mapped to anon function that is not called by name
			(typ, args) match {
				case (arrayType(elementType), List(typedNode(functionNode(cap, args, body, _),_))) =>
					val fId = Util.genRandomName()
					functionCallArgs.getOrElseUpdate(fId, new ListBuffer()).addOne(List(elementType))
					val f = recurseTypedTree(typedNode(functionNode(cap, args, body, metaNode(fId, null)),unknownType()))
					typ = arrayType(f.typ.asInstanceOf[functionType].returnType)
					typedNode(callNode(typedNode(wordNode(id), typ), List(f)), typ)

				case (functionType(typ),_) =>
					typedNode(callNode(typedNode(wordNode(id), functionType(typ)), args.map(recurseTypedTree)), typ)
				case _ =>
					typedNode(callNode(typedNode(wordNode(id), typ), args.map(recurseTypedTree)), typ)
			}
		case typedNode(wordNode(x), ty) =>
			typedNode(wordNode(x), graph.getOrElse(x, voidType()))
		case typedNode(ifNode(c, b, None, id), _) =>
			val typedBody = recurseTypedTree(b)
			typedNode(ifNode(recurseTypedTree(c), typedBody, None, id), typedBody.typ)
		case typedNode(ifNode(c, b, Some(e), id), _) =>
			val typedBody = recurseTypedTree(b)
			typedNode(ifNode(recurseTypedTree(c), typedBody, Some(recurseTypedTree(e)), id), typedBody.typ)

		case typedNode(returnNode(exp),_) =>
			val body = recurseTypedTree(exp)
			typedNode(returnNode(body),body.typ)
		case typedNode(exp, typ) =>
			val content = exp match {
				case arrayNode(elements) => arrayNode(elements.map(recurseTypedTree))
				case comprehensionNode(body, varr, array, None) =>
					val newBody = recurseTypedTree(body)
					val newArray = recurseTypedTree(array)
					comprehensionNode(newBody, varr, newArray, None)
				case comprehensionNode(body, varr, array, Some(filter)) =>
					val newBody = recurseTypedTree(body)
					val newArray = recurseTypedTree(array)
					comprehensionNode(newBody, varr, newArray, Some(recurseTypedTree(filter)))
				case binopNode(op, l, r) => binopNode(op, recurseTypedTree(l), recurseTypedTree(r))
				case unopNode(op, exp) => unopNode(op, recurseTypedTree(exp))
				case mapNode(f, array) => mapNode(recurseTypedTree(f), recurseTypedTree(array))
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
		graph.addOne("println" -> functionType(voidType()))
		graph.addOne("print" -> functionType(voidType()))
		graph.addOne("I" -> functionType(intType()))
		graph.addOne("split" -> functionType(arrayType(stringType())))
	}

	def process(tree: Tree): Tree = {
		println("-------------------- type tracing --------------------")
		val main = tree.asInstanceOf[functionNode]
		injectExternalMethods()
		val body = main.body.asInstanceOf[blockNode]
		val typedBlock = doScope(body)
		functionNode(List(), main.args, typedBlock, main.metaData)
	}
}





/*
* x= 5;
* print x
* y="ss"
* x=y
* */