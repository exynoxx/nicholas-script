import scala.collection.immutable.HashMap
import scala.collection.mutable
import scala.collection.mutable.ListBuffer
import scala.collection.immutable.List

case class typedNode(node: Tree, typ: Type) extends Tree

class TypeTracer {
	val functions = ListBuffer[Tree]()
	val graph = mutable.HashMap[String, Type]()

	def lookupType(left: Type, right: Type): Type = intType()

	def dfs(node: Tree): typedNode = node match {
		case integerNode(_) => typedNode(node, intType())
		case stringNode(_) => typedNode(node, stringType())
		case wordNode(x) => typedNode(node, graph.getOrElse(x, voidType()))
		case arrayNode(elements) => typedNode(node, arrayType())
		case binopNode(op, l, r) =>
			val ll = dfs(l)
			val rr = dfs(r)
			typedNode(binopNode(op, ll, rr), lookupType(ll.typ, rr.typ))
		case functionNode(name, args, body) =>
			functions.append(functionNode(name, args, body))
			typedNode(node, unknownType())
		case assignNode(wordNode(id), body) =>
			val recurs = dfs(body)
			graph.addOne((id -> recurs.typ))
			typedNode(assignNode(wordNode(id), recurs), recurs.typ)
		//TODO: fix duplicate
		case reassignNode(wordNode(id), body) =>
			val recurs = dfs(body)
			graph.addOne((id -> recurs.typ))
			typedNode(assignNode(wordNode(id), recurs), recurs.typ)
		case callNode(wordNode(id), args) =>
			val argTypes = args.map(dfs)
			unknownType()
			typedNode(callNode(wordNode(id), argTypes), unknownType())
		case ifNode(c, b, None) =>
			val body = dfs(b)
			typedNode(ifNode(dfs(c), body, None), body.typ)
		case ifNode(c, b, Some(e)) =>
			val body = dfs(b)
			typedNode(ifNode(dfs(c), body, Some(dfs(e))), body.typ)
		case returnNode(exp) =>
			val body = dfs(exp)
			typedNode(returnNode(body), body.typ)
		case x => typedNode(x, voidType())
	}

	def processor(main: functionNode):Tree= {
		var elems = main.body.asInstanceOf[blockNode].children
		val typedBlock = blockNode(elems.map(dfs))
		functionNode(main.name,main.args,typedBlock)
	}
}

/*
* x= 5;
* print x
* y="ss"
* x=y
* */