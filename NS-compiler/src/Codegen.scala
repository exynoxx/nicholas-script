class Codegen {

    var count = 0

    def generateRandomName() = {
        val s = Integer.toString(count, 26)
        count += 1
        s.map((x) => (x + 97).toChar)
    }

    def source(tree: Tree): String = {
        val (pre,code,function) = gen(tree)
        return code
    }

    def nsTypeToC (ty:String) = ty match {
        case "string" => "char *"
        case ty => ty
    }

    def gen(tree: Tree): Tuple3[String,String,String] = {
        tree match {
            case programNode(children, ns) => {
                //return children.map(gen(_)).foldLeft(("", "",""))({ (a, b) => a + b })
                ("","","")
            }

            case ifNode(cond, body, els, ns) => {
                val c = gen(cond)._2
                val b = gen(body)._2
                val elsebody = gen(els)._2
                val condstring = "(" + c + ")\n"
                val ret = "if" + condstring + b + "else" + elsebody
                return ("",ret,"")
            }

            case assignNode(id, body, ns) => {
                val ty = nsTypeToC(ns)
                val b = gen(body)
                val ret = ty + " " + id + " = " + b + ";\n"
                return ("", ret,"")
            }

            case binopNode(left, sign, right, ns) => {
                val newleft = gen(left)
                val newright = gen(right)
                val ret = newleft + sign + newright
                return ("", ret,"")
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
                    return ("",value,"")
                }
            }

            case whileNode(cond, body, ns) => {
                val (_,c,_) = gen(cond)
                val (_,b,_) = gen(body)
                val ret = "while(" + c + ")" + b
                return ("",ret,"")
            }

            case callNode(id, args, ns) => {
                val newargs = args.map(gen(_))
                ("","","")
            }

            case argNode(id,ns) => ("", nsTypeToC(ns) + id, "")

                /*
            case blockNode(children, ns) => {
                val newchildren = children.map(typeAnnotate(_, env))
                var ty: String = null
                newchildren.map((x) => x match {
                    case returnNode(_, ns) => ty = ns
                })
                return blockNode(newchildren, ty)
            }

            case functionNode(args, body, ns) => {
                val newbody = typeAnnotate(body, env)
                return functionNode(args, newbody, newbody.nstype)
            }*/
        }
    }

}
