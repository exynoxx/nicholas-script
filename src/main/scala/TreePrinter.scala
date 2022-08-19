import io.AnsiColor._

class TreePrinter {
	def printMinus(sym: String, depth: Int): String = {
		val spaces = (0 to depth).map(x => sym).mkString("")
		spaces
	}

	def recursion(t: Tree, depth: Int = 0, increment: Int = 4): String = {
		t match {
			case binopNode(op, left, right) => printMinus("-", depth) +
				"binopNode(" + op + ")\n" +
				recursion(left, depth + increment) +
				recursion(right, depth + increment)

			case unopNode(op, exp) => printMinus("-", depth) +
				"binopNode(" + op + ")\n" +
				recursion(exp, depth + increment)

			/*case opNode(b, ns) => b*/

			case assignNode(wordNode(id), b) => printMinus("-", depth) +
				s"assignNode(${GREEN}" + id + s"${RESET})\n" +
				recursion(b, depth + increment)

			case assignNode(id, b) => printMinus("-", depth) +
				"assignNode(" + id + ")\n" +
				recursion(b, depth + increment)

			case reassignNode(id, b) => printMinus("-", depth) +
				"reassignNode(" + id + ")\n" +
				recursion(b, depth + increment)

			case arrayNode(elem) => printMinus("-", depth) +
				"arrayNode()\n" +
				elem.map(e => recursion(e, depth + increment)).mkString

			case accessNode(array, idx) => printMinus("-", depth) +
				"accessNode()\n" +
				recursion(array, depth + increment) +
				recursion(idx, depth + increment)

			case blockNode(children) => printMinus("-", depth) +
				"blockNode(#children=" + children.length + ")\n" +
				children.map(x => recursion(x, depth + increment)).mkString("")

			case callNode(f, args) => printMinus("-", depth) +
				"callNode()\n" +
				recursion(f, depth + increment) +
				printMinus("-", depth + increment) + "ARGs\n" +
				args.map(e => recursion(e, depth + increment)).mkString("")

			case ifNode(c, b, Some(e), id) => printMinus("-", depth) +
				"ifNode(" + id + ")\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment) +
				recursion(e, depth + increment)
			case ifNode(c, b, _, id) => printMinus("-", depth) +
				"ifNode(" + id + ")\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
			case typedNode(node, typ) => printMinus("-", depth) +
				s"${YELLOW}typedNode(" + typ + s")${RESET}\n" +
				recursion(node, depth + increment)
			case returnNode(exp) => printMinus("-", depth) +
				"returnNode()\n" +
				recursion(exp, depth + increment)

			case functionNode(captured, args, body, null) =>
				printMinus("-", depth) +
					"functionNode(null)\n" +
					printMinus("-", depth + increment) + "CAPTURED\n" +
					captured.map(e => recursion(e, depth + increment)).mkString("") +
					printMinus("-", depth + increment) + "ARGs\n" +
					args.map(e => recursion(e, depth + increment)).mkString("") +
					printMinus("-", depth + increment) + "BODY\n" +
					recursion(body, depth + increment)

			case functionNode(captured, args, body, metaNode(name, extractName)) => printMinus("-", depth) +
				"functionNode(" + name + ", " + extractName + ")\n" +
				printMinus("-", depth + increment) + "CAPTURED\n" +
				captured.map(e => recursion(e, depth + increment)).mkString("") +
				printMinus("-", depth + increment) + "ARGs\n" +
				args.map(e => recursion(e, depth + increment)).mkString("") +
				recursion(body, depth + increment)
			case mapNode(f, array) => printMinus("-", depth) +
				"mapNode()\n" +
				recursion(f, depth + increment) +
				recursion(array, depth + increment)
			case comprehensionNode(body, variable, array, filter) => printMinus("-", depth) +
				"comprehensionNode\n" +
				recursion(body, depth + increment) +
				recursion(variable, depth + increment) +
				recursion(array, depth + increment) +
				recursion(filter.getOrElse(nullLeaf()), depth + increment)
			case lambdaNode(captured, args, body) => printMinus("-", depth) +
				"lambdaNode()\n" +
				printMinus("-", depth + increment) + "CAPTURED\n" +
				captured.map(e => recursion(e, depth + increment)).mkString("") +
				printMinus("-", depth + increment) + "ARGs\n" +
				args.map(e => recursion(e, depth + increment)).mkString("") +
				recursion(body, depth + increment)
			case sequenceNode(l) => printMinus("-", depth) +
				"SEQUENCE() ########################\n" +
				l.map(e=>recursion(e, depth + increment)).mkString("\n")
			case x => printMinus("-", depth) +
				x.toString + "\n"
		}
	}

	def print(t: Tree): Unit = {
		println(t)
		println(recursion(t, 1))
	}
}