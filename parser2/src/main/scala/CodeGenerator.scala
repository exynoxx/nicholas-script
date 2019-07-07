class CodeGenerator {

	case class codeblock(before: String = "", ret: String = "", after: String = "", funcdef: String = "", funcImpl: String = "")

	def convertType(s: String): String = s match {
		case "string" => "char *"
		case z => z
	}

	def recurse(AST: Tree): codeblock = {
		AST match {
			case valueNode(value, ns) => codeblock(ret = "value")
			case binopNode(l, r, o, ns) =>
				val codeblock(_, ll, _, _, _) = recurse(l)
				val codeblock(_, rr, _, _, _) = recurse(r)
				val codeblock(_, oo, _, _, _) = recurse(o)
				codeblock(ret = ll + oo + rr)
			case opNode(op, _) => codeblock(ret = op)
			case assignNode(id, body, ns) =>
				val ty = convertType(ns)
				val codeblock(pre, rett, post, fdef, fimpl) = recurse(body)
				val line = if (!body.isInstanceOf[functionNode]) ty + " " + id + " = " + rett + ";\n" else ""
				codeblock("", pre + line + post, "", fdef, fimpl)
			case functionNode(id, args, body, ns) =>
				val fargs = args.map(e => recurse(e))
					.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString
				val fdef = ns + " " + id + "(" + fargs + ") \n"
				val codeblock(pre, rett, post, adef, aimpl) = recurse(body)
				val fimpl = pre + post + post
				codeblock(funcdef = fdef + ";\n" + adef, funcImpl = fimpl + aimpl)
			case argNode(name, ns) => codeblock(ret = convertType(ns) + " " + name)
			case blockNode(children, ns) =>
				val b = children.map(e => recurse(e))
				val pre = b.map { case codeblock(pre, l, post, fdef, fimpl) => pre }.mkString
				val line = b.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString
				val post = b.map { case codeblock(pre, l, post, fdef, fimpl) => post }.mkString
				val fdef = b.map { case codeblock(pre, l, post, fdef, fimpl) => fdef }.mkString
				val fimpl = b.map { case codeblock(pre, l, post, fdef, fimpl) => fimpl }.mkString
				codeblock(pre, pre + line + post, fdef, fimpl)
		}
	}

	def gen(AST: Tree): String = {
		val codeblock(pre, ret, post, fdef, fimpl) = recurse(AST)
		val main1 = "int main (int arc, char **argv) {\n"
		val main2 = pre + ret + post
		val main3 = "}\n"
		fdef + fimpl + main1 + main2 + main3
	}

}
