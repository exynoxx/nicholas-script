class Codegen {

    var count = 0

    def generateRandomName() = {
        count += 1
        "var" + count
    }

    def source(tree: Tree): String = {
        val (pre, code, function) = gen(tree)
        return function + pre + code
    }

    def nsTypeToC(ty: String) = ty match {
        case "string" => "char *"
        case ty => ty
    }

    //(pre,code,function)
    def gen(tree: Tree): Tuple3[String, String, String] = {
        tree match {
            case programNode(children, ns) => {
                var (pre, code, f) = ("", "", "")
                children.map((x) => {
                    val (a, b, c) = gen(x)
                    pre += a
                    code += b
                    f += c
                })
                (pre, code, f)
            }

            case ifNode(cond, body, els, ns) => {
                val c = gen(cond)._2
                val (prebody,codebody,functionbody) = gen(body)
                val (preelse,codeelse,functionelse) = gen(els)
                val condstring = "(" + c + ")\n"
                val ret = "if" + condstring + codebody + "else" + codeelse
                return (prebody+preelse, ret, functionbody+functionelse)
            }

            case assignNode(id, body, ns) => {
                val ty = nsTypeToC(ns)
                val (pre, code, f) = gen(body)
                val ret = body match {
                    case functionNode(_,_,_,_) => ""
                    case _ => ty + " " + id + " = " + code + ";\n"
                }
                return (pre, ret, f)
            }

            case binopNode(left, sign, right, ns) => {
                val (preleft, codeleft, _) = gen(left)
                val (preright, coderight, _) = gen(right)
                val code = codeleft + sign + coderight
                return (preleft + preright, code, "")
            }

            case valueNode(value, string, variable, ns) => {
                if (string) {
                    val rname = generateRandomName()
                    val size = value.length() - 2
                    val alloc = "char *" + rname + " = (char *) malloc (" + size + ");\n"
                    val copy = "strcpy(" + rname + ", " + value + ");\n"
                    val pre = alloc + copy
                    val code = rname
                    //String post = "free (" + rname + ");\n";
                    return (pre, code, "")
                } else {
                    return ("", value, "")
                }
            }

            case whileNode(cond, body, ns) => {
                val (_, c, _) = gen(cond)
                val (_, b, f) = gen(body)
                val ret = "while(" + c + ")" + b
                return ("", ret, f)
            }

            case callNode(id, args, ns, child) => {
                val pt1 = id
                var pt2 = ""
                var pt3 = "("
                args.map((arg) => {
                    val (pre, code, _) = gen(arg)
                    pt2 += pre
                    pt3 += code
                })
                val pt4 = if (child) ")" else ");\n"
                val ret = pt1 + pt3 + pt4
                (pt2, ret, "")
            }

            //function only
            case argNode(id, ns) => ("", nsTypeToC(ns) + " " + id, "")

            case functionNode(id, args, body, ns) => {
                var ret = ns + " " + id + "("
                args.map((arg) => {
                    val (_, c, _) = gen(arg)
                    ret += c + ","
                })
                ret = ret.substring(0, ret.length - 1) + ")"
                val (_, b, f) = gen(body)
                ret += b + f

                return ("", "", ret)
            }

            case blockNode(children, ns) => {
                var (code, f) = ("{\n", "")
                val newchildren = children.map((x) => {
                    val (a, b, c) = gen(x)
                    code += a + b
                    f += c
                })
                code += "}\n"
                return ("", code, f)
            }
            case _ => ("", "", "")
        }
    }

}
