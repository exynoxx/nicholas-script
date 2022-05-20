import scala.collection.immutable.HashMap
import scala.collection.mutable
import scala.collection.mutable.ListBuffer
import scala.collection.immutable.List
class TypeTracer {

	var edges = ListBuffer[(String, String, Integer)]()
	val functions = ListBuffer[Tree]()

	//match other than wordnode
	def findEdges(elements:List[Tree]) = elements.foreach(dfs)

	//DFS
	def dfs(node:Tree):String = node match {
		case integerNode(_) => "INT"
		case stringNode(_) => "STR"
		case wordNode(x) => x
 		case arrayNode(elements) => "ARR"
		case binopNode(op,l,r) =>
			edges.addOne((dfs(l),op,edges.length))
			edges.addOne((dfs(r),op,edges.length))
			op
		case functionNode(name,args,body) =>
			functions.append(functionNode(name,args, body))
			name
		case assignNode(wordNode(id),body) =>
			edges.addOne((dfs(body),id,edges.length))
			"===="
		//TODO: fix duplicate
		case reassignNode(wordNode(id),body) =>
			edges.addOne((dfs(body),id,edges.length))
			"==#=="
		case callNode(wordNode(id),args) =>
			args.foreach(x=>edges.addOne((dfs(x),id,edges.length)))
			id
		case _ => "!!!"
	}


	//edges are inverted
	def constructGraph(): Map[String,List[(String,Integer)]] =
		edges.groupBy(_._2).map{case (k,v) => (k,v.map{case(u,_,w)=>(u,w)}.toList)}


		def determineType():mutable.HashMap[String,Type] = {
			val types = new  mutable.HashMap[String,Type]()

			val graph = constructGraph()
			graph.keys.foreach(u => {
				if (!types.contains(u)) {
					types.addOne(u,recursiveDetermineType(graph,types,u,1000))
				}
			})
			types
		}

		def recursiveDetermineType(graph:Map[String,List[(String,Integer)]],types:mutable.HashMap[String,Type], u:String, count:Int) : Type = {
			val list = graph(u)
			val (v,w) = list.filter(_._2 < count).maxBy(_._2)
			types.getOrElse(v,recursiveDetermineType(graph, types, v, w))
		}

	def processor(main:functionNode) = {
		findEdges(main.body.asInstanceOf[blockNode].children)
		println(edges)
		println(edges.groupBy(_._2))
		println(determineType().toString)
	}
}

/*
* x= 5;
* print x
* y="ss"
* x=y
* */