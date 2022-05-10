import java.io.{File, PrintWriter}
import scala.io.Source
import sys.process._

object main {

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val alltext = bufferedSource.getLines().mkString("\n")
		bufferedSource.close
		alltext
	}

	def writeFile(filename: String, content: String) = {
		val file = new File(filename)
		if (!file.exists()) {
			file.createNewFile()
		}
		val writer = new PrintWriter(file)
		writer.write(content)
		writer.close()

	}

	def parseString(parser: Parser, s: String): Tree = {
		val inn = "{" + s + "}"
		parser.parse(parser.expression, inn) match {
			case parser.Success(t, _) =>
				println("success")
				t
			case x => println("Error in .parse: " + x)
				nullLeaf()
		}
	}

	def main(args: Array[String]): Unit = {
		val parser = new Parser
		val printer = new TreePrinter

		val typeChecker = new TypeChecker
		val codeGen = new CodeGenCpp

		/*val printer = new TreePrinter
		val treeAugmenter = new TreeAugmenter
		*/
		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.cpp"

		//val in = "1+2*3 & (5/2) * (1+2)"
		//val in = "a=[1,2^3,1+2*3 & 5/2];b=1+1"
		//val in = "[];[1];[1,2,3]"
		//val in = "a={1+1}*5;4*{1+1};c=[abc=1,2,3,4]"
		//val in = "first=+/[1,2,b=3,+/c];c=1+2;([1])*10"
		//val in = "{1+1}/a;+/a;f/a; {1} 1 1"
		//val in = "a;f;f/a; {f} 1 1;{x}/[1,2,3];f 1 1 + 1; + 1 a 3 4"
		//val in = "a=[1,2,b=3];c=a$2;[0]$0; d=[e=[1,2],[3,4]]$0"
		//val in = "+/l10+2; !10; !true; 1+!10; ({x+5}[1,2,3]);"
		//val in = "print=_NS_print;six = {a+b*c} 1 2 3; false = !true; fac = !5; list = {x*2}/[1,2,3]; a = ([1]*10)+1; print a"
		val in = "println=_NS_println; mult2 = {x*2}; x = !false; println x; x = mult2 5; println x; x = mult2 [1,1]; println x; x = x+1; println x"
		//val in = "x=5;x=[1,\"2\",3];x=1+1;"
		val ast = parseString(parser, in)
		printer.print(ast)
		println("-----------txt source:---------------------")
		val outputString = codeGen.stringiFy(typeChecker.typecheck(ast))
		println(outputString);
		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		writeFile(outputFile, outputString)

		/*val typedTree = typeChecker.typecheck(AST)
		//printer.print(typedTree)
		val augmentedTree = treeAugmenter.augment(typedTree)
		printer.print(augmentedTree)
		val ret = codeGen.gen(augmentedTree)
		writeFile(outputFile, ret)
		*/
		val f = ("g++ " + outputFile + " -o out/output").!
		println(f)


	}
}