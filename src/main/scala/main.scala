import Util.{readFile, writeFile}

import java.io.{File, PrintWriter}
import scala.io.Source
import sys.process._

object main {



	def main(args: Array[String]): Unit = {

		val printer = new TreePrinter

		val parser = new Parser
		val stage1 = new TreeAugmenter
		val stage2 = new TypeTracer
		val stage3 = new TypeInliner
		val codeGen = new CodeGenCpp

		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.cpp"

		//val in = "even = {(x%2==0)?true:false};println even 5;println even 6;println even 7;"
		//val in = "y=1+1;f={k*2+l};x=\"str\";x=y;non=f x 1;y=5*x;x=y;print x;"
		//val in = "println 1+1; fib = { (n <= 1) ? 1 : (fib n-1) + (fib n-2)}; println (fib 35); x=[1,2,3];x$1+x$2;"
		val in = "one = [1]*10; two = {x*2}; two/[1,2,3]; {x*2}/[1,2,3];"

		var AST = parser.process(in)
		printer.print(AST)
		AST = stage1.process(AST)
		printer.print(AST)
		AST = stage2.process(AST)
		printer.print(AST)
		AST = stage3.process(AST)
		printer.print(AST)
		AST = stage1.process(AST)
		printer.print(AST)
		val output = codeGen.process(AST)
		println(output)
		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		writeFile(outputFile, output)

		val f = ("g++ " + outputFile + " -o out/output").!
		println(f)


		//val tests = new Tests
		//tests.run()


	}
}