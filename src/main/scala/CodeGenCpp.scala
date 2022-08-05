import scala.+:

class CodeGenCpp {

	var preMainFunctions = ""

	def convertType(ty: Type): String = ty match {
		case intType() => "int"
		case boolType() => "bool"
		case stringType() => "std::string"
		case arrayType(ty) => "std::shared_ptr<std::vector<" + convertType(ty) + ">>"
		case _ => "auto"
	}

	def recurseTree(t: Tree): String = t match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => "std::string(" + x + ")"
		case wordNode(x) => x
		case binopNode(op, left, right) => recurseTypedTree(left) + op + recurseTypedTree(right)
		case reassignNode(id, b) => recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case accessNode(array, idx) => recurseTypedTree(array) + "[" + recurseTypedTree(idx) + "]"
		case blockNode(elem) => //"{\n" + elem.map(recurse).mkString("",";\n",";\n") + "\n}\n"
			elem.map {
				case typedNode(ifNode(a, b, c, d), ty) => recurseTypedTree(ifNode(a, b, c, d))
				case typedNode(nullLeaf(), _) => ""
				case x => recurseTypedTree(x) + ";\n"
			}.mkString("{\n", "", "}\n")
		case returnNode(exp) => "return " + recurseTypedTree(exp)
		case libraryCallNode(fname, expr) => fname + "(" + expr.map(recurseTypedTree).mkString(",") + ")"
		case callNode(wordNode(f), args) =>
			f + "(" + args.map(recurseTypedTree).mkString(",") + ")"

		/*case callNode(functionNode(_, fargs, body), args) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "(" + fargs.map(x => "_NS_var " + recurseTypedTree(x)).mkString(",") + ")"
			preMainFunctions += recurseTypedTree(body)
			id + "(" + args.map(recurseTypedTree).mkString(",") + ")"*/

		case ifNode(cond, body, els, resultId) =>
			val result = convertType(body.asInstanceOf[typedNode].typ) + " " + resultId + ";\n"

			val elsString = els match {
				case None => ""
				case Some(x) => "\nelse\n" + recurseTypedTree(x)
			}

			result + "if (" + recurseTypedTree(cond) + ")\n" + recurseTypedTree(body) + elsString

		case nullLeaf() => ""
		//case sequenceNode(l) => l.map(recurse).mkString(";\n")

		case comprehensionNode(body, _, array, Some(filterOption)) =>
			"_NS_map_filter(" + recurseTypedTree(array) + "," + recurseTypedTree(body) + "," + recurseTypedTree(filterOption) + ")"
		case comprehensionNode(body, _, array, None) =>
			"_NS_map_filter(" + recurseTypedTree(array) + "," + recurseTypedTree(body) + ",null)"
		case mapNode(f, array) =>
			"_NS_map(" + recurseTypedTree(f) + "," + recurseTypedTree(array) + ")"
		case x => throw new IllegalArgumentException(x.toString)
	}

	def recurseTypedTree(t: Tree): String = t match {
		case typedNode(functionNode(captured, args, body, meta), ty) =>
			val metaNode(name, _) = meta
			preMainFunctions += convertType(ty) + " " + name

			val stringArgs = (captured ++ args).map {
				case typedNode(x, ty) => convertType(ty) + " " + recurseTree(x)
			}.mkString(",")

			preMainFunctions += "(" + stringArgs + ")"
			preMainFunctions += recurseTypedTree(body)
			"1"

		case typedNode(assignNode(id, b), ty) => "auto " + recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case typedNode(arrayNode(elements), ty) =>
			val stringElements = elements.map(recurseTypedTree).mkString(",")
			"new std::make_shared<" + convertType(ty) + ">({" + stringElements + "})"
		case typedNode(node, _) => recurseTree(node)
		case x => recurseTree(x)
	}

	def process(t: Tree): String = {

		val mainBody = t match {
			case functionNode(_, _, blockNode(elem), _) =>
				val nsMain = "int _NS_main ()" + recurseTree(blockNode(elem))
				val main = "int main() {\n _NS_main();\nreturn 0;\n}"
				nsMain + main
		}
		"#include \"std.cpp\"\n" + preMainFunctions + mainBody
	}
}
