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

		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.cpp"

		val in = "even = {(x%2==0)?true:false};println even 5;println even 6;println even 7;"

		val ast = parseString(parser, in)
		printer.print(ast)
		println("-----------txt source:---------------------")
		val outputString = codeGen.stringiFy(typeChecker.typecheck(ast))
		println(outputString);
		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		writeFile(outputFile, outputString)

		val f = ("g++ " + outputFile + " -o out/output").!
		println(f)

		//val tests = new Tests
		//tests.run()


	}
}