class CodeGenJS {

	def recurse(tree: Tree): String = {
		tree match {
			case assignNode(id, body, deff, _, ns) =>
				deff match {
					case true => "var " + id + " = " + recurse(body) + "\n"
					case false => id + " = " + recurse(body) + "\n"
				}
			case opNode(body, ns) => body
			case binopNode(numbers, ops, _, ns) =>
				val opsString = ops.map(x => recurse(x)) ++ List("")
				val numbersString = numbers.map(x => recurse(x))
				numbersString.zip(opsString).map { case (x, y) => x + y }.mkString
			case valueNode(value, ns) => value
			case ifNode(c, b, elsebody, ns) =>
				val s1 = "if (" + recurse(c) + ") {\n"
				val s2 = recurse(b)
				val s3 = "}\n"

				val s4 = elsebody match {
					case Some(ss) => "else {\n" + recurse(ss) + "}\n"
					case None => ""
				}
				s1 + s2 + s3 + s4
			case whileNode(c, b, ns) =>
				val s1 = "while (" + recurse(c) + ") {\n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3
			case argNode(name, ns) => name

			case functionNode(id, args, body, ns) =>
				val s1 = "var " +id+ " = function(" + args.map(x => recurse(x)).mkString(",") + ") {\n"
				val s2 = recurse(body)
				val s3 = "}\n"
				s1 + s2 + s3

			case blockNode(children, ns) =>
				children.map(x => recurse(x)).mkString

			case callNode(id, args, deff, ns) =>
				val ret = id + "(" + args.map(x => recurse(x)).mkString(",") + ")"
				deff match {
					case true => ret + "\n"
					case false => ret
				}

			case returnNode(body, ns) => "return " + recurse(body) + "\n"

			case lineNode(text, ns) => text

			case arrayNode(elements, ns) =>
				"[" + elements.map(x => recurse(x)).mkString(",") + "]"

			case rangeNode(valueNode(a, _), valueNode(b, _), ns) => "Array.from(new Array(" + b + "-" + a + "), (x,i) => i + " + a + ")"

			case accessNode(name, idx, _) => name+"["+idx+"]"
			case x => "//" + x.toString + "\n"
		}
	}

	def gen(AST: Tree): String = {
		recurse(AST)
	}

}
