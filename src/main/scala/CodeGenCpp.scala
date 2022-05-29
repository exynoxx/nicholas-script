import scala.+:

class CodeGenCpp {

	var preMainFunctions = ""

	def convertType(ty: Type): String = ty match {
		case intType() => "int"
		case boolType() => "bool"
		case stringType() => "std::string"
		case arrayType(_) => "std::shared_ptr<std::vector<std::variant<int,bool,std::string>>>"
		case _ => "auto"
	}

	def recurseTree(t: Tree): String = t match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => "std::string(" + x + ")"
		case wordNode(x) => x
		case binopNode(op, left, right) => recurseTypedTree(left) + op + recurseTypedTree(right)
		case reassignNode(id, b) => recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case arrayNode(elements) =>
			val stringElements = elements.map(recurseTypedTree).mkString(",")
			val vecInit = "new std::vector<std::variant<int,bool,std::string>>({"+stringElements+"})"
			"new " + convertType(arrayType()) + "("+vecInit+")"
		case accessNode(array, idx) => recurseTypedTree(array) + "[" + recurseTypedTree(idx) + "]"
		case blockNode(elem) => //"{\n" + elem.map(recurse).mkString("",";\n",";\n") + "\n}\n"
			elem.map {
				case typedNode(ifNode(a, b, c, d),ty) => recurseTypedTree(ifNode(a, b, c, d))
				case x => recurseTypedTree(x) + ";\n"
			}.mkString("{\n", "", "}\n")
		case returnNode(exp) => "return " + recurseTypedTree(exp)
		case libraryCallNode(fname, expr) => fname + "(" + expr.map(recurseTypedTree(_)).mkString(",") + ")"
		case callNode(wordNode(f), args) =>
			f + "(" + args.map(recurseTypedTree).mkString(",") + ")"

		/*case callNode(functionNode(_, fargs, body), args) =>
			val id = Util.genRandomName()
			preMainFunctions += "_NS_var " + id
			preMainFunctions += "(" + fargs.map(x => "_NS_var " + recurseTypedTree(x)).mkString(",") + ")"
			preMainFunctions += recurseTypedTree(body)
			id + "(" + args.map(recurseTypedTree).mkString(",") + ")"*/

		case ifNode(cond, body, els, _) =>
			val id = Util.genRandomName()
			val result = "_NS_var " + id + " = NULL;\n"

			val elsString = els match {
				case None => ""
				case Some(x) => "\nelse\n" + recurseTypedTree(x)
			}

			"if (" + recurseTypedTree(cond) + ")\n" + recurseTypedTree(body) + elsString

		case nullLeaf() => ""
		//case sequenceNode(l) => l.map(recurse).mkString(";\n")
		case x => x.toString
	}

	def recurseTypedTree(t: Tree): String = t match {
		case typedNode(functionNode(args, body, meta), ty) =>
			val metaNode(name, extractName) = meta
			val id = extractName
			preMainFunctions += convertType(ty) + " " + id

			val stringArgs = args.map {
				case typedNode(x, ty) => convertType(ty) + " " + recurseTree(x)
			}.mkString(",")

			preMainFunctions += "(" + stringArgs + ")"
			preMainFunctions += recurseTypedTree(body)
			"&" + id
		case typedNode(assignNode(id, b), ty) => "auto " + recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case typedNode(node, _) => 	recurseTree(node)
		case x => recurseTree(x)
	}

	def process(t: Tree): String = {

		val mainBody = t match {
			case functionNode(_, blockNode(elem), _) =>
				val nsMain = "int _NS_main ()" + recurseTree(blockNode(elem))
				val main = "int main() {\n _NS_main();\nreturn 0;\n}"
				nsMain + main
		}
		"#include \"std.cpp\"\n" + preMainFunctions + mainBody
	}
}
