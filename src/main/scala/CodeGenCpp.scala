class CodeGenCpp {

	var preMainFunctions = ""


	def recurse(t: Tree): String = t match {
		case integerNode(x) => "_NS_create_var("+x+")"
		case boolNode(x) => "_NS_create_var("+x+")"
		case stringNode(x) => "_NS_create_var("+x+")"
		case wordNode(x) => x
		case binopNode(op, left, right) => {
			val nativeFunction = op match {
				case "+" => "_NSadd"
				case "-" => "_NSminus"
				case "*" => "_NSmult"
				case "/" => "_NSdiv"
			}
			nativeFunction + "(" + recurse(left) + "," + recurse(right) + ")"
		}
		case assignNode(id, b) => "auto " + recurse(id) + "=" + recurse(b)
		case reassignNode(id, b) => recurse(id) + "=" + recurse(b)
		case arrayNode(elements) => "_NS_create_var({" + elements.map(recurse).mkString(",") + "})"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case blockNode(elem) => "{\n" + elem.map(recurse).mkString("",";\n",";\n")  + "\n}\n"
		case functionNode(args, body) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "("+args.map(x=>"_NS_var "+recurse(x)).mkString(",")+")"
			preMainFunctions += recurse(body)
			"_NS_create_var(&"+id+")"
		case returnNode(exp) => "return " + recurse(exp)
		case libraryCallNode(fname, expr) => fname+"("+expr.map(recurse).mkString(",")+")"
		case callNode(wordNode(f),args) => f+"("+args.map(recurse).mkString(",") +")"
		//case sequenceNode(l) => l.map(recurse).mkString(";\n")
		case x => x.toString
	}

	def stringiFy(t:Tree):String={
		val mainBody = t match {
			case functionNode(_,block) => "int main ()" + recurse(block)
		}
		preMainFunctions + mainBody
	}
}
