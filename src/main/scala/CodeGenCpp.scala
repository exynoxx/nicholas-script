class CodeGenCpp {
	def recurse(t: Tree): String = t match {
		case integerNode(x) => "NSvar("+x+")"
		case boolNode(x) => "NSvar("+x+")"
		case stringNode(x) => x
		case wordNode(x) => x
		case binopNode(op, left, right) => recurse(left) + op + recurse(right)
		case assignNode(id, b) => recurse(id) + "=" + recurse(b)
		case arrayNode(elements) => "NSvar({" + elements.map(recurse).mkString(",") + "})"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case blockNode(elem) => "blocknode{\n" + elem.map(recurse).mkString(";\n") + "}\n"
		case functionNode(args, blockNode(elements)) => ""
		case x => x.toString

	}
}
