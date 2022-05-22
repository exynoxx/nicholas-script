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
		val typeTracer = new TypeTracer
		val inliner = new TypeInliner
		val codeGen = new CodeGenCpp

		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.cpp"

		//val in = "even = {(x%2==0)?true:false};println even 5;println even 6;println even 7;"
		//val in = "y=1+1;f={k*2+l};x=\"str\";x=y;f x 1;y=5*x;x=y;print x;"
		val in = "println 1+1; fib = { (n <= 1) ? 1 : (fib n-1) + (fib n-2)}; println (fib 35);"

		println("---------------------- parsed ----------------------")
		val ast = parseString(parser, in)
		printer.print(ast)
		println("--------------------- argumented --------------------")
		val argmented = typeChecker.typecheck(ast)
		printer.print(argmented)
		println("-------------------- type traced --------------------")
		val typed = typeTracer.process(argmented.asInstanceOf[functionNode])
		printer.print(typed)
		println("---------------------- inlined ----------------------")
		val inlined = inliner.process(typed)
		printer.print(inlined)
		val outputString = codeGen.stringiFy(inlined)
		println(outputString)

		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		writeFile(outputFile, outputString)

		val f = ("g++ " + outputFile + " -o out/output").!
		println(f)


		//val tests = new Tests
		//tests.run()


	}
}