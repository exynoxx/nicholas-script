class CodeGenJS {


	def recurse(node: Tree): String = node match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => x
		case wordNode(x) => x
		case binopNode(op, left, right) => recurse(left) + op + recurse(right)
		case arrayNode(elements) => "[" + elements.map(recurse).mkString(",") + "]"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case blockNode(elem) => elem.map(recurse).mkString("{\n", ";\n", ";\n}\n")
		case returnNode(exp) => "return " + recurse(exp)
		case libraryCallNode(fname, expr) => fname + "(" + expr.map(recurse).mkString(",") + ")"
		case callNode(wordNode(f), args) => f + "(" + args.map(recurse).mkString(",") + ")"

		/*case callNode(functionNode(_, fargs, body), args) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "(" + fargs.map(x => "_NS_var " + recurseTypedTree(x)).mkString(",") + ")"
			preMainFunctions += recurseTypedTree(body)
			id + "(" + args.map(recurseTypedTree).mkString(",") + ")"*/

		case ifNode(cond, body, els, _) =>
			val elsString = els match {
				case None => ""
				case Some(x) => "\nelse\n" + recurse(x)
			}
			"if (" + recurse(cond) + ")\n" + recurse(body) + elsString

		case functionNode(captured,args, body, _) =>
			"(" + (captured++args).map(recurse).mkString(",") + ") => " + recurse(body)
		case assignNode(id, b) => recurse(id) + "=" + recurse(b)
		case reassignNode(id, b) => recurse(id) + "=" + recurse(b)
		case typedNode(node, _) => recurse(node)
		case mapNode(f,array) => recurse(array) + ".map(" + recurse(f) + ")"
		case comprehensionNode(body,variable,array,Some(filter)) =>
			recurse(array) + ".filter(" + recurse(variable) + "=>" + recurse(filter) + ").map(" + recurse(variable) + "=>" + recurse(body)+")"
		case nullLeaf() => ""
		case x => x.toString
	}

	def process(tree: Tree): String = {
		val std = Util.readFile("src/main/scala/std.js") + "\n"
		val main = tree match {
			case functionNode(_,_, blockNode(elem), _) => elem.map(recurse).mkString(";\n")

		}
		std+main
	}
}

