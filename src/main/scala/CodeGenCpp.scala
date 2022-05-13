class CodeGenCpp {

	var preMainFunctions = ""


	def recurse(t: Tree): String = t match {
		case integerNode(x) => "_NS_create_var(" + x + ")"
		case boolNode(x) => "_NS_create_var(" + x + ")"
		case stringNode(x) => "_NS_create_var(" + x + ")"
		case wordNode(x) => x
		case binopNode(op, left, right) => {
			val nativeFunction = op match {
				case "+" => "_NSadd"
				case "-" => "_NSminus"
				case "*" => "_NSmult"
				case "/" => "_NS"
				case "%" => "_NS"
				case "&" | "&&" => "_NS"
				case "|" | "||" => "_NS"
				case "**" | "^" => "_NS"
			}
			nativeFunction + "(" + recurse(left) + "," + recurse(right) + ")"
		}
		case assignNode(id, b) => "auto " + recurse(id) + "=" + recurse(b)
		case reassignNode(id, b) => recurse(id) + "=" + recurse(b)
		case arrayNode(elements) => "_NS_create_var({" + elements.map(recurse).mkString(",") + "})"
		case accessNode(array, idx) => recurse(array) + "[" + recurse(idx) + "]"
		case blockNode(elem) => "{\n" + elem.map(recurse).mkString("", ";\n", ";\n") + "\n}\n"
		case functionNode(args, body) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "(" + args.map(x => "_NS_var " + recurse(x)).mkString(",") + ")"
			preMainFunctions += recurse(body)
			"_NS_create_var(&" + id + ")"
		case returnNode(exp) => "return " + recurse(exp)
		case libraryCallNode(fname, expr) => fname + "(" + expr.map(recurse).mkString(",") + ")"
		case callNode(wordNode(f), args) =>
			val fname = args.length match {
				case 0 => "f0"
				case 1 => "f1"
				case 2 => "f2"
			}
			f + "->value->" + fname + "(" + args.map(recurse).mkString(",") + ")"

		case callNode(functionNode(fargs, body), args) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "(" + fargs.map(x => "_NS_var " + recurse(x)).mkString(",") + ")"
			preMainFunctions += recurse(body)
			id + "(" + args.map(recurse).mkString(",") + ")"
		//case sequenceNode(l) => l.map(recurse).mkString(";\n")
		case x => x.toString
	}

	def stringiFy(t: Tree): String = {
		val insideMainIncludes = "_NS_addition_ops[8 * 1 + 1] = &_NS_std_adder;_NS_addition_ops[8 * 1 + 4] = &_NS_int_list_adder;_NS_addition_ops[8 * 4 + 1] = &_NS_list_int_adder;_NS_minus_ops[8 * 1 + 1] = &_NS_std_minus;_NS_minus_ops[8 * 1 + 4] = &_NS_int_list_minus;_NS_minus_ops[8 * 4 + 1] = &_NS_list_int_minus;_NS_mult_ops[8 * 1 + 1] = &_NS_std_mult;_NS_mult_ops[8 * 4 + 1] = &_NS_list_int_mult;_NS_mult_ops[8 * 1 + 4] = &_NS_int_list_mult;"
		val mainBody = t match {
			case functionNode(_, blockNode(elem)) =>
				val elemNoReturn = elem.map {
					case returnNode(x) => x
					case x => x
				}
				"int main (){\n" +
					insideMainIncludes.replaceAll(";", ";\n") +
					elemNoReturn.map(recurse).mkString("", ";\n", ";\n") +
					"return 0;\n" +
				"\n}\n"
		}
		"#include \"std.cpp\"\n" + preMainFunctions + mainBody
	}
}
