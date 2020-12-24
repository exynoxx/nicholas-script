import scala.collection.mutable.ListBuffer

class CodeGenRust {

	def convertType(t: String): String = {
		t match {
			case "actualstring" => "String"
			case "string" => "String"
			case "int" => "i32"
			case Util.arrayTypePattern(ty) => "Vec<" + convertType(ty) + ">"
			case x => x
		}
	}

	def convertArgType(t: String): String = {
		t match {
			case "string" => "&mut String"
			case x => convertType(x)
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
				val flatList = ListBuffer.from(numbersString.zip(opsString).flatMap(tup => List(tup._1, tup._2)))


				var i = 1
				while (i < flatList.size - 1) {
					if (flatList(i) == "**") {
						val tmp = flatList(i - 1) + ".pow(" + flatList(i + 1) + ")"
						flatList.remove(i - 1)
						flatList.remove(i - 1)
						flatList.remove(i - 1)
						flatList.insert(i - 1, tmp)
					}
					i += 2
				}
				"(" + flatList.mkString + ")"
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
				val s1 = "while " + recurse(c) + " {\n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3

			case argNode(name, ns) => name + ":" + convertArgType(ns)

			case functionNode(id, args, body, ns) =>

				val retTy = convertType(ns) match {
					case "void" => ""
					case x => "-> " + x
				}

				val s0 = "fn " + id
				val s1 = "(" + args.map(x => recurse(x)).mkString(",") + ")" + retTy + " {\n"
				val s2 = recurse(body)
				val s3 = "}\n"
				s0 + s1 + s2 + s3

			case blockNode(children, ns) => children.map(x => recurse(x)).mkString

			case callNode(id, args, deff, ns) =>

				val stringArgs = args.map{
					case valueNode(name,"string") => "&mut "+name
					case x => recurse(x)
				}

				val ret = id + "(" + stringArgs.mkString(",") + ")"
				deff match {
					case true => ret + ";\n"
					case false => ret
				}

			case returnNode(body, ns) => recurse(body) + "\n"

			case lineNode(text, ns) => text + "\n"

			case arrayNode(elements, ns) => "vec![" + elements.map(x => recurse(x)).mkString(",") + "]"

			case rangeNode(valueNode(a, _), valueNode(b, _), ns) => "(" + a + ".." + b + ").collect()"

			case accessNode(name, idx, _) =>
				val idxString = recurse(idx)
				val postfix = idx match {
					case valueNode(_,_) => ""
					case binopNode(_,_,_,_) => " as usize"
				}
				name + "[" + idxString + postfix +"]"

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
