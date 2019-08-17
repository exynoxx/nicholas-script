class TreePrinter {
	def printMinus(sym: String, depth: Int): String = {
		val spaces = (0 to depth).map(x => sym).mkString("")
		spaces
	}

	def recursion(t: Tree, depth: Int = 0, increment: Int = 6): String = {
		t match {
			case valueNode(value, ns) => printMinus("-", depth) +
				"valueNode(" + value + "," + ns + ")\n"
			case binopNode(numbers, ops, idx, ns) => printMinus("-", depth) +
				"binopNode(idx=" + idx + ",ns=" + ns + ")\n" +
				printMinus("-", depth + increment) + numbers + "\n" +
				printMinus("-", depth + increment) + ops + "\n"
			case opNode(b, ns) => b
			case assignNode(id, b, deff, idx, ns) => printMinus("-", depth) +
				"assignNode(" + id + ", definition=" + deff + ", idx=" + idx + ", ns=" + ns + ")\n" +
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
			case whileNode(c, b, ns) => printMinus("-", depth) +
				"whileNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
			case functionNode(id, args, b, ns) => printMinus("-", depth) +
				"functionNode(" + id + ")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",") +
				recursion(b, depth + increment)
			case argNode(name, ns) => printMinus("-", depth) +
				"argNode(" + name + ":" + ns + ")\n"
			case returnNode(body, ns) => printMinus("-", depth) +
				"returnNode()\n" +
				recursion(body, depth + increment, increment)
			case callNode(id, args, deff, ns) => printMinus("-", depth) +
				"callNode(" + id + ", definition=" + deff + ")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",")
			case allocNode(name, size, ns) => printMinus("-", depth) +
				"allocNode(" + name + ",size=" + size + ")\n"
			case freeNode(variable, ns) => printMinus("-", depth) +
				"freeNode(" + variable + ")\n"

			case lineNode(text, ns) => printMinus("-", depth) +
				"lineNode(" + text + ")\n"

		}
	}

	def print(t: Tree): Unit = {
		println(t)
		println(recursion(t, 1))
	}
}