
class CodeGenerator {

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

        val st = numbers.slice(0, ops.length).zip(ops).map {
          case (valueNode(n, "int"), opNode(o, _)) => n + o
          case _ => ""
        }.mkString + (numbers.last match {
          case valueNode(n, _) => n
        })
        codeblock(alloc+prestrings, st, "", "", "")

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
            codeblock("", pre+line, post, fdef, fimpl)
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
