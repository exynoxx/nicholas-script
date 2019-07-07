class TreePrinter {
	def printMinus(sym: String, depth: Int): String = {
		val spaces = (0 to depth).map(x => sym).mkString("")
		spaces
	}

	def recursion(t: Tree, depth: Int = 0, increment: Int = 6): String = {
		t match {
			case valueNode(value, ns) => printMinus("-", depth) +
				"valueNode(" + value + "," + ns + ")\n"
			case binopNode(l, r, o, ns) => printMinus("-", depth) +
				"binopNode(" + recursion(o) + ")\n" +
				recursion(l, depth + increment) +
				recursion(r, depth + increment)
			case opNode(b, ns) => b
			case assignNode(id, b, ns) => printMinus("-", depth) +
				"assignNode(" + id + ")\n" +
				recursion(b, depth + increment)
			case blockNode(children, ns) => {
				val s = printMinus("-", depth) + "blockNode(" + children.length + ")\n"
				val pc = children.map(x => recursion(x, depth + increment)).mkString("")
				s + pc
			}
			case ifNode(c, b, Some(e), ns) => printMinus("-", depth) +
				"ifNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment) +
				recursion(e, depth + increment)
			case ifNode(c, b, None, ns) => printMinus("-", depth) +
				"ifNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
			case whileNode(c,b,ns) => printMinus("-", depth) +
				"whileNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
		}
	}

	def print(t: Tree): Unit = {
		println(t)
		println(recursion(t, 1))
	}
}