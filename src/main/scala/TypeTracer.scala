import scala.collection.mutable
import scala.collection.mutable.ListBuffer

case class typedNode(node: Tree, typ: Type) extends Tree

class TypeTracer {
	val functionCallArgs = mutable.HashMap[String,ListBuffer[List[Type]]]()
	var graph = mutable.HashMap[String, Type]()

	def lookupType(left: Type, right: Type): Type = (left,right) match {
		case (intType(),intType()) => intType()
		case (boolType(),boolType()) => boolType()
		case (stringType(),stringType()) => stringType()
		case (arrayType(a), arrayType(b)) => arrayType(a)
		case _ => intType()
	}

	def dfs1(node: Tree): typedNode = node match {
		case integerNode(_) => typedNode(node, intType())
		case stringNode(_) => typedNode(node, stringType())
		case wordNode(x) => typedNode(node, graph.getOrElse(x, voidType()))
		case arrayNode(elements) =>
			val typedElements = elements.map(dfs1)
			typedNode(arrayNode(typedElements), arrayType(typedElements.map(_.typ)))
		case binopNode(op, l, r) =>
			val ll = dfs1(l)
			val rr = dfs1(r)
			typedNode(binopNode(op, ll, rr), lookupType(ll.typ, rr.typ))
		case functionNode(id,_,_) =>
			typedNode(node, unknownType())
		case assignNode(wordNode(id), body) =>
			val recurs = dfs1(body)
			graph.addOne(id -> recurs.typ)
			typedNode(assignNode(wordNode(id), recurs), recurs.typ)
		//TODO: fix duplicate
		case reassignNode(wordNode(id), body) =>
			val recurs = dfs1(body)
			graph.addOne(id -> recurs.typ)
			typedNode(assignNode(wordNode(id), recurs), recurs.typ)
		case callNode(wordNode(id), args) =>
			val argTypes = args.map(dfs1)
			functionCallArgs.getOrElseUpdate(id,new ListBuffer()).addOne(argTypes.map(_.typ))
			typedNode(callNode(wordNode(id), argTypes), unknownType())
		case ifNode(c, b, None) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, None), body.typ)
		case ifNode(c, b, Some(e)) =>
			val body = dfs1(b)
			typedNode(ifNode(dfs1(c), body, Some(dfs1(e))), body.typ)
		case returnNode(exp) =>
			val body = dfs1(exp)
			typedNode(returnNode(body), body.typ)
		case x => typedNode(x, voidType())
	}

	def recurseTypedTree(node: Tree): Tree = node match {
		case wordNode(x) =>
			typedNode(node, graph.getOrElse(x, voidType()))
		case arrayNode(elements) => arrayNode(elements.map(recurseTypedTree))
		case binopNode(op, l, r) =>
			binopNode(op,recurseTypedTree(l),recurseTypedTree(r))
		//paste back in
		case functionNode(name, args, body) =>
			val elementOption = functionCallArgs.get(name)
			elementOption match {
				case Some(listOfListOfArgs:ListBuffer[List[Type]]) =>
					//TODO spawn copies
					val argTypes = listOfListOfArgs(0)
					val namedArgs = args.zip(argTypes).map{case (node,typ)=>typedNode(node,typ)}
					//TODO send recursively
					val graphCopy = graph.clone()
					namedArgs.foreach{case typedNode(wordNode(id),ty) => graph.addOne(id->ty)}
					val ret = functionNode(name,namedArgs,doScope(body.asInstanceOf[blockNode]))
					graph = graphCopy
					ret
				case None => node
			}
		case assignNode(wordNode(id), body) =>
			assignNode(wordNode(id), recurseTypedTree(body))
		case reassignNode(wordNode(id), body) =>
			assignNode(wordNode(id), recurseTypedTree(body))
		case ifNode(c, b, None) =>
			ifNode(recurseTypedTree(c),recurseTypedTree(b),None)
		case ifNode(c, b, Some(e)) =>
			ifNode(recurseTypedTree(c),recurseTypedTree(b),Some(recurseTypedTree(e)))
		case returnNode(exp) =>
			returnNode(recurseTypedTree(exp))


		case typedNode(callNode(wordNode(id), args), ty) =>
			if (ty != unknownType()) return node
			typedNode(callNode(wordNode(id), args.map(recurseTypedTree)), unknownType())

		case x => typedNode(x, voidType())
	}


	//TODO: more than 2 runs
	def doScope(scope: blockNode): Tree = {
		val firstRun = blockNode(scope.children.map(dfs1))
		blockNode(firstRun.children.map(dfs1))
	}

	def process(main: functionNode):Tree= {
		var elems = main.body.asInstanceOf[blockNode].children
		val typedBlock = blockNode(elems.map(dfs1))
		val typedBlock2 =  blockNode(elems.map(recurseTypedTree))
		functionNode(main.name,main.args,typedBlock2)
	}
}

/*
* x= 5;
* print x
* y="ss"
* x=y
* */