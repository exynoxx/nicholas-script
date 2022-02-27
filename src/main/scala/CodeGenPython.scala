class CodeGenPython {
	def recurse(t: Tree): String = t match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => x
		case wordNode(x) => x
		//TODO: python operator prescedence different from parse
		case binopNode(op, left, right) => recurse(left) + op + recurse(right)
		case assignNode(id, b) => recurse(id) + "=" + recurse(b)
		case arrayNode(elements) => "[" + elements.map(recurse).mkString(",") + "]"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case functionNode(args, blockNode(elements)) => ""

	}
}
