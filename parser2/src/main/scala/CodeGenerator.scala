class CodeGenerator {

	case class codeblock(before: String = "", ret: String = "", after: String = "", funcdef: String = "", funcImpl: String = "")

	var visitedBlock = false

	def convertType(s: String): String = s match {
		case "string" => "char *"
		case z => z
	}

	def recurse(AST: Tree): codeblock = {
		AST match {
			case valueNode(value, ns) => codeblock(ret = value)
			case binopNode(l, r, o, ns) =>
				val codeblock(_, ll, _, _, _) = recurse(l)
				val codeblock(_, rr, _, _, _) = recurse(r)
				val codeblock(_, oo, _, _, _) = recurse(o)
				codeblock(ret = ll + oo + rr)
			case opNode(op, _) => codeblock(ret = op)
			case assignNode(id, body, ns) =>
				//TODO INT ASSIGN NO TYPE
				val ty = convertType(ns)
				val codeblock(pre, rett, post, fdef, fimpl) = recurse(body)
				val line = if (!body.isInstanceOf[functionNode]) ty + " " + id + " = " + rett + ";\n" else ""
				codeblock("", pre + line + post, "", fdef, fimpl)
			case functionNode(id, args, body, ns) =>
				val fargs = args.map(e => recurse(e))
					.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString(",")
				val fdef = ns + " " + id + "(" + fargs + ")"
				val codeblock(pre, rett, post, adef, aimpl) = recurse(body)
				val fimpl = rett
				codeblock("", "", "", fdef + ";\n" + adef, fdef + fimpl + aimpl)
			case argNode(name, ns) => codeblock(ret = convertType(ns) + " " + name)
			case blockNode(children, ns) =>
				//TODO fix this
				if (visitedBlock) {
					val b = children.map(e => recurse(e))
					val pre = b.map { case codeblock(pre, l, post, fdef, fimpl) => pre }.mkString
					val line = b.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString
					val post = b.map { case codeblock(pre, l, post, fdef, fimpl) => post }.mkString
					val fdef = b.map { case codeblock(pre, l, post, fdef, fimpl) => fdef }.mkString
					val fimpl = b.map { case codeblock(pre, l, post, fdef, fimpl) => fimpl }.mkString
					var content = "{\n" + pre + line + post + "}\n"
					codeblock("", content, "", fdef, fimpl)
				} else {
					visitedBlock = true
					val b = children.map(e => recurse(e))
					val pre = b.map { case codeblock(pre, l, post, fdef, fimpl) => pre }.mkString
					val line = b.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString
					val post = b.map { case codeblock(pre, l, post, fdef, fimpl) => post }.mkString
					val fdef = b.map { case codeblock(pre, l, post, fdef, fimpl) => fdef }.mkString
					val fimpl = b.map { case codeblock(pre, l, post, fdef, fimpl) => fimpl }.mkString
					var content = pre + line + post
					codeblock("", content,"", fdef, fimpl)
				}

			case ifNode(c, b, Some(elsbody), ns) =>
				val codeblock(_, con, _, _, _) = recurse(c)
				val codeblock(_, body, _, f1, f2) = recurse(b)
				val codeblock(_, els, _, f3, f4) = recurse(elsbody)
				val line = "if (" + con + ")" + body + els
				codeblock("", line, "", f1 + f3, f2 + f4)
			case ifNode(c, b, None, ns) =>
				val codeblock(_, con, _, _, _) = recurse(c)
				val codeblock(_, body, _, f1, f2) = recurse(b)
				val line = "if (" + con + ")" + body
				codeblock("", line, "", f1, f2)
			case returnNode(body, ns) =>
				val codeblock(p, b, _, _, _) = recurse(body)
				codeblock(ret = p + "return " + b + ";\n")
		}
	}

	def gen(AST: Tree): String = {
		val codeblock(pre, ret, post, fdef, fimpl) = recurse(AST)
		val main0 = fdef + fimpl
		val main1 = "int main (int arc, char **argv) {\n"
		val main2 = pre + ret + post
		val main3 = "}\n"
		main0 + main1 + main2 + main3
	}

}
