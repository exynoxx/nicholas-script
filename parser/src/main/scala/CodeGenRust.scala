

class CodeGenRust {

	def convertType(t: String): String = {
		t match {
			case "actualstring" => "String"
			case "string" => "String"
			case "int" => "i32"
			case Util.arrayTypePattern(ty) => "Vec<"+convertType(ty)+">"
			case x => x
		}
	}

	def recurse(tree: Tree): String = {
		tree match {
			case assignNode(id, body, deff, _, ns) =>
				val end = ns match {
					case "actualstring" => recurse(body) + ".to_string();\n"
					case _ => recurse(body) + ";\n"
				}
				val ss = deff match {
					case true => "let mut " + id + ":" + convertType(ns) + " = " + end
					case false => id + " = " + end
				}
				ss
			case opNode(body, ns) => body
			case binopNode(numbers, ops, _, ns) =>
				val opsString = ops.map(x => recurse(x)) ++ List("")
				val numbersString = numbers.map(x => recurse(x))
				numbersString.zip(opsString).map { case (x, y) => x + y }.mkString
			case valueNode(value, ns) => value
			case ifNode(c, b, elsebody, ns) =>
				val s1 = "if " + recurse(c) + " {\n"
				val s2 = recurse(b)
				val s3 = "}\n"

				val s4 = elsebody match {
					case Some(ss) => "else {\n" + recurse(ss) + "}\n"
					case None => ""
				}
				s1 + s2 + s3 + s4
			case whileNode(c, b, ns) =>
				val s1 = "while " + recurse(c) + ") \n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3
			case argNode(name, ns) => name + ":" + convertType(ns)

			case functionNode(id, args, body, ns) =>

				val retS = convertType(ns) match {
					case "void" => ""
					case x => "-> " + x
				}

				val s0 = "fn " + id
				val s1 = "(" + args.map(x => recurse(x)).mkString(",") + ")" + retS + " {\n"
				val s2 = recurse(body)
				val s3 = "}\n"
				s0 + s1 + s2 + s3

			case blockNode(children, ns)
			=>
				children.map(x => recurse(x)).mkString

			case callNode(id, args, deff, ns)
			=>
				val ret = id + "(" + args.map(x => recurse(x)).mkString(",") + ")"
				deff match {
					case true => ret + ";\n"
					case false => ret
				}

			case returnNode(body, ns)
			=> recurse(body) + "\n"

			case lineNode(text, ns)
			=> text

			case arrayNode(elements, ns)
			=>
				"vec![" + elements.map(x => recurse(x)).mkString(",") + "]"

			case rangeNode(valueNode(a, _), valueNode(b, _), ns)
			=> "(" + a + ".." + b + ").collect()"

			case accessNode(name, idx, _)
			=> name + "[" + idx + "]"
			case x => "//" + x.toString + "\n"
		}
	}

	def gen(AST: Tree): String = {
		val s1 = "fn main(){\n"
		val s2 = recurse(AST)
		val s3 = "}\n"
		s1 + s2 + s3
	}

}
