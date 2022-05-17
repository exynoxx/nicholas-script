import scala.collection.immutable.HashMap
import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class TypeTracer {

	var edges = ListBuffer[(String, Any, Integer)]()
	val functions = ListBuffer[Tree]()

	//match other than wordnode
	def findEdges(elements:List[Tree]) = elements.foreach(dfs)

	//DFS
	def dfs(node:Tree):Type = node match {
		case integerNode(_) => intType()
		case stringNode(_) => stringType()
		case wordNode(x) => transparentType(x)
 		case arrayNode(elements) => arrayType()
		case binopNode(op,l,r) => compositeType(op,dfs(l),dfs(r))
		case functionNode(name,args,body) =>
			functions.append(functionNode(name,args, body))
			functionType(name)
		case assignNode(wordNode(id),body) =>
			val bodyTy = dfs(body)
			edges += (id,bodyTy,edges.length)
			bodyTy
		//TODO: fix duplicate
		case reassignNode(wordNode(id),body) =>
			val bodyTy = dfs(body)
			edges += (id,bodyTy,edges.length)
			bodyTy
		case callNode(wordNode(id),args) =>
			args.foreach(x => edges += (dfs(x),id,edges.length))
			unknownType()
	}


	def constructGraph(): Unit ={
		val graph = new mutable.HashMap[String,mutable.HashMap[String,Integer]]()
		edges.foreach((a,b,w) => graph.getOrElseUpdate(a,new mutable.HashMap()))
		//(graph.edges.groupBy(_.source).map
		//          { case (k,v) => (k, v.map {_.destination}) }).toList
	}

	def determineType():HashMap[String,Type] = {

	}



}

/*
* x= 5;
* print x
* y="ss"
* x=y
* */