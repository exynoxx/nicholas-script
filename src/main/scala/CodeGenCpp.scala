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
		case blockNode(elem) => "\n{\n" + elem.map(recurse).mkString(";\n") + "\n}"
		case functionNode(args, body) => "NSvar([=]("+args.map(recurse).mkString(",")+")"+recurse(body)+")"
		case libraryCallNode(fname, expr) => fname+"("+expr.map(recurse).mkString(",")+")"
		case x => x.toString
	}

	def stringiFy(t:Tree):String=t match {
		case functionNode(_,blockNode(elem)) => "int main () {\n" + elem.map(recurse).mkString(";\n") + "\n}"
	}
}
