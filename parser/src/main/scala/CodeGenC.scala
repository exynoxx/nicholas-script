import scala.collection.immutable.HashMap

class CodeGenC {

	/*

def calculateScopeAllocSize(blockBody: List[Tree], symbol: HashMap[String, String]): List[Tree] = {
	var idx = 0
	val newbody = blockBody.map {
		case assignNode(id, valueNode(name, "actualstring"), deff, _, ns) =>
			val ret = assignNode(id, valueNode(name, "actualstring"), deff, idx, ns)
			idx += name.length - 2
			ret
		case assignNode(id, binopNode(l, r, o, "string"), deff, _, ns) =>
			val sizel: Int = l match {
				case valueNode(n, "actualstring") => n.length - 2
				case _ => (symbol.get(id)).toString.toInt
			}
			val sizer: Int = r match {
				case valueNode(n, "actualstring") => n.length - 2
				case _ => (symbol.get(id)).toString.toInt
			}
			val ret = assignNode(id, binopNode(l, r, o, "string"), deff, idx, ns)
			idx += sizel + sizer
		case x => x
	}
	val allocName = Util.genRandomName()
	var ret = List[Tree](allocNode(allocName, idx))
	newbody.foreach {
		case returnNode(body, ns) => ret ++= List(freeNode(allocName)) ++ List(returnNode(body, ns))
		case x: Tree => ret ++= List(x)
	}
	ret
}

*/

	def annotateChildren(children: List[Tree], symbol: HashMap[String, Int]): List[Tree] = {
		var symClone = symbol
		var tmpIdx = 0

		//iter children
		val newchildren = children.map(e => {
			val (newElem, sym, size) = annotateSize(e, symClone, tmpIdx)
			symClone = symClone ++ sym
			tmpIdx += size
			newElem
		})

		//malloc culmultative size from above
		val allocName = Util.genRandomName()
		var ret = List[Tree](allocNode(allocName, tmpIdx))

		//contains return? free before
		var foundReturn = false
		newchildren.foreach {
			case returnNode(body, ns) =>
				ret ++= List(freeNode(allocName)) ++ List(returnNode(body, ns))
				foundReturn = true
			case x: Tree => ret ++= List(x)
		}

		//no return? insert free anyway
		if (!foundReturn) {
			ret ++= List(freeNode(allocName))
		}
		ret
	}

	def annotateSize(AST: Tree, symbol: HashMap[String, Int] = HashMap(), idx: Int = 0): (Tree, HashMap[String, Int], Int) = {
		AST match {
			case assignNode(id, valueNode(name, "actualstring"), deff, _, _) =>
				val size = name.length - 2
				val ret = assignNode(id, valueNode(name, "actualstring"), deff, idx, "string")
				(ret, symbol + (id -> size), size)

			case assignNode(id, binopNode(numbers, ops, _, ns), deff, _, "string") =>
				val allsizes = numbers.map {
					case valueNode(n, "string") => symbol.getOrElse(n, 0)
					case _ => 0
				}.sum
				val ret = assignNode(id, binopNode(numbers, ops, idx, ns), deff, 0, "string")
				(ret, symbol + (id -> allsizes), allsizes)

			case assignNode(id, functionNode(_, args, blockNode(children, ns), fns), deff, idx, ans) =>
				val newchildren = annotateChildren(children, symbol)
				(functionNode(id, args, blockNode(newchildren, ns), fns), symbol, 0)
			case blockNode(children, ns) =>
				val newchildren = annotateChildren(children, symbol)
				(blockNode(newchildren, ns), symbol, 0)
			case ifNode(cond, body, els, ns) =>
				val newbody = body match {
					case blockNode(children, ns) => blockNode(annotateChildren(children, symbol), ns)
					case x => x
				}
				val newels = els match {
					case Some(blockNode(children, ns)) => Some(blockNode(annotateChildren(children, symbol), ns))
					case x => x
				}
				(ifNode(cond, newbody, newels, ns), symbol, 0)
			case whileNode(cond, body, ns) =>
				(whileNode(cond, body, ns), symbol, 0)

			case x => (x, symbol, 0)
		}
	}

	case class codeblock(before: String = "", ret: String = "", after: String = "", funcdef: String = "", funcImpl: String = "")

	var blockRecursionDepth = 0

	def recurse(AST: Tree, blockAllocName: String): codeblock = {
		AST match {
			case valueNode(value, ns) => codeblock(ret = value)

			//TODO reassignment to string not activated
			case binopNode(numbers, ops, idx, ns) =>
				val id = Util.genRandomName()
				val alloc = ns match {
					case "string" => "char *" + id + " = " + blockAllocName + "+" + idx + ";\n"
					case _ => ""
				}
				var first = true
				var prestrings = numbers.map {
					case valueNode(n, "string") =>
						val catfunc = first match {
							case true =>
								first = false
								"strcpy"
							case false => "strcat"
						}
						catfunc + "(" + id + "," + n + ");\n"
					case _ => ""
				}.mkString

				val ret = ns match {
					case "string" => id
					case _ =>
						numbers.slice(0, ops.length).zip(ops).map {
							case (valueNode(n, "int"), opNode(o, _)) => n + o
							case _ => ""
						}.mkString + (numbers.last match {
							case valueNode(n, _) => n
						})
				}
				codeblock(alloc + prestrings, ret, "", "", "")

			/*
		case assignNode(id, binopNode(l, r, o, bns), deff, idx, ns) =>
			val codeblock(_, ll, _, _, _) = recurse(l, blockAllocName)
			val codeblock(_, rr, _, _, _) = recurse(r, blockAllocName)
			val codeblock(_, oo, _, _, _) = recurse(o, blockAllocName)

			val retLine = ns match {
				case "string" =>
					val alloc = "char *" + id + " = " + blockAllocName + "+" + idx + ";\n"
					val concat = "strcpy(" + id + "," + ll + ");\nstrcat(" + id + "," + rr + ");\n"
					alloc + concat
				case _ =>
					val body = ll + oo + rr
					Util.convertType(ns) + " " + id + " = " + body + ";\n"
			}
			codeblock(ret = retLine)

			 */
			case assignNode(id, valueNode(value, "actualstring"), deff, idx, ns) =>
				val alloc = "char *" + id + " = " + blockAllocName + "+" + idx + ";\n"
				val copyline = "strcpy(" + id + "," + value + ");\n"
				codeblock(ret = alloc + copyline)
			case opNode(op, _) => codeblock(ret = op)

			case assignNode(id, body, deff, idx, ns) =>
				val ty = Util.convertType(ns)
				val codeblock(pre, rett, post, fdef, fimpl) = recurse(body, blockAllocName)
				body match {
					case functionNode(_, _, _, _) => codeblock("", "", "", fdef, fimpl)
					case _ =>
						val line =
							if (deff)
								ty + " " + id + " = " + rett + ";\n"
							else
								id + " = " + rett + ";\n"
						codeblock("", pre + line, post, fdef, fimpl)
				}

			case functionNode(id, args, body, ns) =>
				val fargs = args.map(e => recurse(e, blockAllocName))
					.map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString(",")
				val fdef = Util.convertType(ns) + " " + id + "(" + fargs + ")"
				val codeblock(pre, rett, post, adef, aimpl) = recurse(body, blockAllocName)
				val fimpl = rett
				codeblock("", "", "", fdef + ";\n" + adef, fdef + fimpl + aimpl)
			case argNode(name, ns) => codeblock(ret = Util.convertType(ns) + " " + name)
			case blockNode(children, ns) =>
				blockRecursionDepth += 1

				var tmpAllocName: String = null
				var str = ""
				var free = ""
				var retStatement: Tree = null

				val filteredChildren = children.filter {
					case returnNode(body, ns) =>
						retStatement = returnNode(body, ns)
						false
					case allocNode(name, size, ns) =>
						str = "char *" + name + "= (char *) malloc (" + size + ");\n"
						tmpAllocName = name
						false
					case freeNode(variable, ns) =>
						free = "free(" + variable + ");\n"
						false
					case _ => true
				}
				val b = filteredChildren.map(e => recurse(e, tmpAllocName))
				b.map { case codeblock(pre, l, post, fdef, fimpl) => str += pre + l }
				val post = b.map { case codeblock(pre, l, post, fdef, fimpl) => post }.mkString
				val fdef = b.map { case codeblock(pre, l, post, fdef, fimpl) => fdef }.mkString
				val fimpl = b.map { case codeblock(pre, l, post, fdef, fimpl) => fimpl }.mkString

				blockRecursionDepth -= 1

				var content = str + post + free
				if (blockRecursionDepth > 0) {
					val codeblock(_, retText, _, _, _) = recurse(retStatement, tmpAllocName)
					content += retText
					content = "{\n" + content + "}\n"
				}
				codeblock("", content, "", fdef, fimpl)

			case ifNode(c, b, Some(elsbody), ns) =>
				val codeblock(_, con, _, _, _) = recurse(c, blockAllocName)
				val codeblock(_, body, _, f1, f2) = recurse(b, blockAllocName)
				val codeblock(_, els, _, f3, f4) = recurse(elsbody, blockAllocName)
				val line = "if (" + con + ")" + body + "else " + els
				codeblock("", line, "", f1 + f3, f2 + f4)
			case ifNode(c, b, None, ns) =>
				val codeblock(_, con, _, _, _) = recurse(c, blockAllocName)
				val codeblock(_, body, _, f1, f2) = recurse(b, blockAllocName)
				val line = "if (" + con + ")" + body
				codeblock("", line, "", f1, f2)
			case whileNode(c, b, ns) =>
				val codeblock(_, con, _, _, _) = recurse(c, blockAllocName)
				val codeblock(_, body, _, f1, f2) = recurse(b, blockAllocName)
				val line = "while (" + con + ")" + body
				codeblock("", line, "", f1, f2)
			case returnNode(body, ns) =>
				val codeblock(p, b, _, _, _) = recurse(body, blockAllocName)
				codeblock(ret = p + "return " + b + ";\n")
			case callNode(id, args, deff, ns) =>
				val argstring = args.map(e => recurse(e, blockAllocName))
					.map { case codeblock(pre, l, post, _, _) => l }.mkString(",")
				val line1 = id + "(" + argstring + ")"
				val line2 = if (deff) ";\n" else ""
				val finalline = line1 + line2
				codeblock(ret = finalline)
		}
	}

	def gen(AST: Tree): String = {

		var blockAllocName: String = null
		AST match {
			case blockNode(children, ns) =>
				children.foreach {
					case allocNode(name, _, _) => blockAllocName = name
					case _ => null
				}
		}

		val codeblock(pre, ret, post, fdef, fimpl) = recurse(AST, blockAllocName)
		val include = "#include <stdlib.h>\n#include <stdio.h>\n#include <string.h>\n"
		val main0 = fdef + fimpl
		val main1 = "int main (int arc, char **argv) {\n"
		val main2 = pre + ret + post
		val main3 = "}\n"
		include + main0 + main1 + main2 + main3
	}

}
