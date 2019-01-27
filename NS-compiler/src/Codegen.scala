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
                val b = gen(body)._2
                val elsebody = gen(els)._2
                val condstring = "(" + c + ")\n"
                val ret = "if" + condstring + b + "else" + elsebody
                return ("", ret, "")
            }

            case assignNode(id, body, ns) => {
                val ty = nsTypeToC(ns)
                val (pre, code, f) = gen(body)
                val ret = ty + " " + id + " = " + code + ";\n"
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
                val (_, b, _) = gen(body)
                val ret = "while(" + c + ")" + b
                return ("", ret, "")
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
            case argNode(id, ns) => ("", nsTypeToC(ns) + id, "")

                /*
            case functionNode(args, body, ns) => {
                val newbody = typeAnnotate(body, env)
                return functionNode(args, newbody, newbody.nstype)
            }
            */

            case blockNode(children, ns) => {
                var (code,f) = ("{\n", "")
                val newchildren = children.map((x) => {
                    val (a,b,c) = gen(x)
                    code += a+b
                    f += c
                })
                return ("",code,f)
            }
        }
    }

}
