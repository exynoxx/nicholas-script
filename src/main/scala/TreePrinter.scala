class TreePrinter {
	def printMinus(sym: String, depth: Int): String = {
		val spaces = (0 to depth).map(x => sym).mkString("")
		spaces
	}
	
	def printNS(s:Type): String ={
		if (s == null){
			return "null"
		} else {
			return s.toString
		}
	}

	def recursion(t: Tree, depth: Int = 0, increment: Int = 4): String = {
		t match {
			/*case valueNode(value, ns) => printMinus("-", depth) +
				"valueNode(" + value + "," + ns + ")\n"*/
			case binopNode(op, left, right) => printMinus("-", depth) +
				"binopNode(" + op + ")\n" +
				recursion(left, depth + increment) +
				recursion(right, depth + increment)

			/*case opNode(b, ns) => b*/
			case assignNode(id, b) => printMinus("-", depth) +
				"assignNode(" + id + ")\n" +
				recursion(b, depth + increment)

			case arrayNode(elem) => printMinus("-", depth) +
				"arrayNode()\n" +
				elem.map(e => recursion(e, depth + increment)).mkString

			case blockNode(children) => printMinus("-", depth) +
				"blockNode(#children=" + children.length + ")\n" +
				children.map(x => recursion(x, depth + increment)).mkString("")

			/*
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
			case forNode(v, a, b, ns) => printMinus("-", depth) +
				"forNode\n" +
				recursion(v, depth + increment) +
				recursion(a, depth + increment) +
				recursion(b, depth + increment)
			case functionNode(id, args, b, ns) => printMinus("-", depth) +
				"functionNode(" + id + ",ns=" + printNS(ns) + ")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",") +
				recursion(b, depth + increment)
			case argNode(name, ns) => printMinus("-", depth) +
				"argNode(" + name + ":" + printNS(ns) + ")\n"
			case returnNode(body, ns) => printMinus("-", depth) +
				"returnNode()\n" +
				recursion(body, depth + increment, increment)
			case callNode(id, args, deff, ns) => printMinus("-", depth) +
				"callNode(" + id + ", definition=" + deff + ", ns=" + printNS(ns) + ")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",")
			case lineNode(text, ns) => printMinus("-", depth) +
				"lineNode(" + text + ")\n"
			case arrayNode(null, ns) => printMinus("-", depth) +
				"arrayNode()\n"
			case arrayNode(elem, ns) => printMinus("-", depth) +
				"arrayNode(" + elem + "," + printNS(ns) + ")\n" +
				elem.map(e => recursion(e, depth + increment)).mkString
			case rangeNode(l, r, ns) => printMinus("-", depth) +
				"rangeNode()\n" +
				recursion(l, depth + increment) +
				recursion(r, depth + increment)
			case anonNode(args, b, ns) => printMinus("-", depth) +
				"anonNode(ns="+printNS(ns)+")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",") +
				recursion(b, depth + increment)
			case objectNode(name, rows, ns) => printMinus("-", depth) +
				"ObjectNode(" + name + ", " + printNS(ns) + ")\n" +
				rows.map(e => recursion(e, depth + increment)).mkString(",")
			case objectElementNode(name, ns) => printMinus("-", depth) +
				"ObjectElementNode(" + name + "," + printNS(ns) + ")\n"
			case objectInstansNode(name, args, ns) => printMinus("-", depth) +
				"ObjectInstansNode(" + name + ", " + printNS(ns) + ")\n" +
				args.map(e => recursion(e, depth + increment)).mkString(",")
			case objectAssociatedFunctionNode(name, functions, ns) => printMinus("-", depth) +
				"ObjectAssociatedFunctionNode(" + name + ")\n" +
				functions.map(e => recursion(e, depth + increment)).mkString(",")*/

			case overrideNode(op, f, ns) => printMinus("-", depth) +
				"OverrideNode()\n" +
				printMinus("-", depth+ increment) + op + "\n" +
				recursion(f, depth + increment)
			case x => printMinus("-", depth) +
				x.toString + "\n"
		}
	}

	def print(t: Tree): Unit = {
		println(t)
		println(recursion(t, 1))
	}
}