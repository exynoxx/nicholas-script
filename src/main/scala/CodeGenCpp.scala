import scala.+:

class CodeGenCpp {

	var preMainFunctions = ""

	def convertType(ty: Type): String = ty match {
		case intType() => "int"
		case boolType() => "bool"
		case stringType() => "std::string"
		case arrayType(ty) => "std::shared_ptr<std::vector<" + convertType(ty) + ">>"
		case lambdaType(ty,argTypes) => "std::function<"+convertType(ty)+"("+argTypes.map(convertType).mkString(",")+")>"
		case functionType(ret) => convertType(ret)
		case _ => "auto"
	}

	def recurseTree(t: Tree): String = t match {
		case integerNode(x) => x.toString
		case boolNode(x) => x.toString
		case stringNode(x) => "std::string(" + x + ")"
		case wordNode(x) => x
		case binopNode(op, left, right) => recurseTypedTree(left) + op + recurseTypedTree(right)
		case reassignNode(id, b) => recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case accessNode(array, idx) => "(*"+recurseTypedTree(array) + ")[" + recurseTypedTree(idx) + "]"
		case blockNode(elem) => //"{\n" + elem.map(recurse).mkString("",";\n",";\n") + "\n}\n"
			elem.map {
				case typedNode(ifNode(a, b, c, d), ty) => recurseTypedTree(ifNode(a, b, c, d))
				case typedNode(nullLeaf(), _) => ""
				case x => recurseTypedTree(x) + ";\n"
			}.mkString("{\n", "", "}\n")
		case returnNode(exp) => "return " + recurseTypedTree(exp)
		case libraryCallNode(fname, expr) => fname + "(" + expr.map(recurseTypedTree).mkString(",") + ")"
		case callNode(f, args) =>
			recurseTypedTree(f) + "(" + args.map(recurseTypedTree).mkString(",") + ")"

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

			/*      //filter
			  val filteredResultId = Util.genRandomName()
			  val arrayName = recurseTypedTree(array)

			  val s1 = "std::vector<T> " + filteredResultId + ";\n"
			  val s2 = "std::copy_if("+arrayName+"->begin(), "+arrayName+"->end(), std::back_inserter("+filteredResultId+"),"+ recurseTypedTree(filterOption)+");\n"

			  //map
			  val mappedResultId = Util.genRandomName()
			  val s3 = "auto " + mappedResultId + " = new std::vector("+filteredResultId+");\n"
			  val s4 = "std::transform("+mappedResultId+"->begin(), "+mappedResultId+"->end(), "+mappedResultId+"->begin(),"+recurseTypedTree(body)+");\n"
			  s1+s2+s3+s4
      		*/
			"_NS_map_filter(" + recurseTypedTree(array) + "," + recurseTypedTree(body) + "," + recurseTypedTree(filterOption) + ")"
		case comprehensionNode(body, _, array, None) =>
			"_NS_map_filter(" + recurseTypedTree(array) + "," + recurseTypedTree(body) + ",null)"
		case mapNode(f, array) =>
			"_NS_map(" + recurseTypedTree(f) + "," + recurseTypedTree(array) + ")"

		case lambdaNode(_, args, body) =>
			val argTypes = args.map{ case typedNode(_,typ) =>typ}.map(convertType)
			val argConverted = args.map(recurseTypedTree)
			val finalArgs = argTypes.zip(argConverted).map{case (x,y) => x + " " + y}
			"[=](" + finalArgs.mkString(",") + ")" + recurseTypedTree(body)
		case typedNode(_, _) => recurseTypedTree(t)
		case x => throw new IllegalArgumentException(x.toString)
	}


	def recurseTypedTree(t: Tree): String = t match {
		case typedNode(functionNode(captured, args, body, meta), ty) =>
			val metaNode(name, _) = meta

			var preMainString = convertType(ty) + " " + name

			val stringArgs = (captured ++ args).map {
				case typedNode(x, ty) => convertType(ty) + " " + recurseTree(x)
			}.mkString(",")

			preMainString += "(" + stringArgs + ")"
			preMainString += recurseTypedTree(body)
			preMainFunctions += preMainString
			name

		case typedNode(assignNode(id, b), ty) => convertType(ty) + " " + recurseTypedTree(id) + "=" + recurseTypedTree(b)
		case typedNode(arrayNode(elements), ty) =>
			val stringElements = elements.map(recurseTypedTree).mkString(",")
			val elementType = convertType(ty.asInstanceOf[arrayType].elementType)
			"std::make_shared<std::vector<" + elementType + ">>(std::initializer_list<"+elementType+">{" + stringElements + "})"
		case typedNode(node, _) => recurseTree(node)
		case x => recurseTree(x)
	}

	def process(t: Tree): String = {
		val includes = "#include \"std.cpp\"\n"

		val mainBody = t match {
			case functionNode(_, _, blockNode(elem), _) =>
				val nsMain = convertType(elem.last.asInstanceOf[typedNode].typ) + " _NS_main ()" + recurseTree(blockNode(elem))
				val main = "int main() {\n _NS_main();\nreturn 0;\n}"
				nsMain + main
		}
		includes + preMainFunctions + mainBody
	}
}
