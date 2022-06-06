class CodeGenJS {


	def recurse(node: Tree): String = node match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => x
		case wordNode(x) => x
		case binopNode(op, left, right) => recurse(left) + op + recurse(right)
		case arrayNode(elements) => "[" + elements.map(recurse).mkString(",") + "]"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case blockNode(elem) => elem.map(recurse).mkString("{\n", "\n", "}\n")
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

		case functionNode(args, body, meta) =>
			val metaNode(name,extractName) = meta
			name + " = (" + args.map(recurse).mkString(",") + ") => " + recurse(body)
		case assignNode(id, b) => recurse(id) + "=" + recurse(b)
		case reassignNode(id, b) => recurse(id) + "=" + recurse(b)
		case typedNode(node, _) => recurse(node)
		case nullLeaf() => ""
		case x => x.toString
	}

	def process(tree: Tree): String = tree match {
		case functionNode(_, blockNode(elem), _) => elem.map(recurse).mkString("\n")

	}
}

