class CodeGenerator {

    case class codeblock(before: String = "", ret: String = "", after: String = "", funcdef: String = "", funcImpl: String = "")

    var blockRecursionDepth = 0

    def recurse(AST: Tree): codeblock = {
        AST match {
            case valueNode(value, ns) => codeblock(ret = value)
            case binopNode(l, r, o, ns) =>
                val codeblock(_, ll, _, _, _) = recurse(l)
                val codeblock(_, rr, _, _, _) = recurse(r)
                val codeblock(_, oo, _, _, _) = recurse(o)

                if (ns == "string") {
                    val rname = Util.genRandomName()
                    val alloc = "char *" + rname + " = (char *) malloc (strlen(" + ll + ")+strlen("+rr+"));\n"
                    val concat = "strcpy("+rname+","+ll+");\nstrcat("+rname+","+rr+");\n"
                    val free = "free("+rname+");\n"
                    codeblock(alloc+concat,rname,free,"","")
                } else {
                    codeblock(ret = ll + oo + rr)
                }

            case opNode(op, _) => codeblock(ret = op)
            case assignNode(id, body, deff, ns) =>
                val ty = Util.convertType(ns)
                val codeblock(pre, rett, post, fdef, fimpl) = recurse(body)
                body match {
                    case functionNode(_, _, _, _) => codeblock("", "", "", fdef, fimpl)
                    case valueNode(value, "string") => {
                        val size = value.length - 2
                        val mallocline = id + " = (char *) malloc (" + size + ");\n"
                        val copyline = "strcpy("+id+","+value+");\n"
                        val free = "free("+id+");\n"
                        codeblock("",mallocline+copyline,free,fdef,fimpl)
                    }
                    case _ => val line = if (deff) ty + " " + id + " = " + rett + ";\n" else id + " = " + rett + ";\n"
                        codeblock(pre, line, post, fdef, fimpl)
                }

            case functionNode(id, args, body, ns) =>
                val fargs = args.map(e => recurse(e))
                    .map { case codeblock(pre, l, post, fdef, fimpl) => l }.mkString(",")
                val fdef = ns + " " + id + "(" + fargs + ")"
                val codeblock(pre, rett, post, adef, aimpl) = recurse(body)
                val fimpl = rett
                codeblock("", "", "", fdef + ";\n" + adef, fdef + fimpl + aimpl)
            case argNode(name, ns) => codeblock(ret = Util.convertType(ns) + " " + name)
            case blockNode(children, ns) =>
                blockRecursionDepth += 1

                var str = ""
                val b = children.map(e => recurse(e))
                b.map{case codeblock(pre, l, post, fdef, fimpl) => str += pre+l }
                val post = b.map { case codeblock(pre, l, post, fdef, fimpl) => post }.mkString
                val fdef = b.map { case codeblock(pre, l, post, fdef, fimpl) => fdef }.mkString
                val fimpl = b.map { case codeblock(pre, l, post, fdef, fimpl) => fimpl }.mkString

                blockRecursionDepth -= 1

                var content = str + post
                if (blockRecursionDepth > 0) {
                    content = "{\n" + content + "}\n"
                }
                codeblock("", content, "", fdef, fimpl)

            case ifNode(c, b, Some(elsbody), ns) =>
                val codeblock(_, con, _, _, _) = recurse(c)
                val codeblock(_, body, _, f1, f2) = recurse(b)
                val codeblock(_, els, _, f3, f4) = recurse(elsbody)
                val line = "if (" + con + ")" + body + "else " + els
                codeblock("", line, "", f1 + f3, f2 + f4)
            case ifNode(c, b, None, ns) =>
                val codeblock(_, con, _, _, _) = recurse(c)
                val codeblock(_, body, _, f1, f2) = recurse(b)
                val line = "if (" + con + ")" + body
                codeblock("", line, "", f1, f2)
            case whileNode(c, b, ns) =>
                val codeblock(_, con, _, _, _) = recurse(c)
                val codeblock(_, body, _, f1, f2) = recurse(b)
                val line = "while (" + con + ")" + body
                codeblock("", line, "", f1, f2)
            case returnNode(body, ns) =>
                val codeblock(p, b, _, _, _) = recurse(body)
                codeblock(ret = p + "return " + b + ";\n")
            case callNode(id, args, deff, ns) =>
                val argstring = args.map(e => recurse(e))
                    .map { case codeblock(pre, l, post, _, _) => l }.mkString(",")
                val line1 = id + "(" + argstring + ")"
                val line2 = if (deff) ";\n" else ""
                val finalline = line1 + line2
                codeblock(ret = finalline)
        }
    }

    def gen(AST: Tree): String = {
        val codeblock(pre, ret, post, fdef, fimpl) = recurse(AST)
        val include = "#include <stdio.h>\n#include <string.h>\n"
        val main0 = fdef + fimpl
        val main1 = "int main (int arc, char **argv) {\n"
        val main2 = pre + ret + post
        val main3 = "}\n"
        include + main0 + main1 + main2 + main3
    }

}
