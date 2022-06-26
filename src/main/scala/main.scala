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
		val stage3 = new TypeAugmenter
		val stage4 = new TypeInliner
		val codeGen = new CodeGenJS

		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.js"

		//val in = "even = {(x%2==0)?true:false};println (even 5);println (even 6);println (even 7);"
		//val in = "y=1+1;f={k*2+l};x=\"str\";x=y;non=f x 1;y=5*x;x=y;print x;"
		//val in = "println 1+1; fib = { (n <= 1) ? 1 : (fib n-1) + (fib n-2)}; println (fib 35); x=[1,2,3];x$1+x$2;"

		//val in = "one = [1]*10; a = [1,2,3]; [1,2,3]; b=a$0;"
		//val in = "f={x}; a = f 1; b = {x*2}; f = a; b = f + 10; 0"
		//val in = "one = [1]*10; two = {x*2}; [1,2,3] two; [1,2,3] {x-1}; a = one$0; 1 + [1,1,1]; [2,2,2]+2"

		//anon func val in ="println ({ (n <= 1) ? 1 : (fib n-1) + (fib n-2)} 50);0"

		//val in = "[1,1,1] {x*10} |> println;0"

		//val in = "qsort = {[x$0]}; (qsort [1,2,3]) |> println"
		//val in = "qsort = {}; (qsort [1,2,3]) |> println"

		//val in = "qsort = { (qsort [x|x:list|x<list$0]) + [list$0] + (qsort [x|x:list|x>=list$0])}; [5,2,3,6,1,4] |> qsort |> println"
		//val in = "qsort = { (qsort [x|x:list|x<list$0]) ++ [list$0] ++ (qsort [x|x:list|x>=list$0]) }; qsort [1,2,3];"
		//val in = "qsort = { if x? > 0 | true | false }; qsort [1,2,3];"
		val in = "qsort = { if list?<=1 | list | (qsort [x|x:list|x<list$0]) ++ [list$0] ++ (qsort [x|x:list|x>=list$0]) }; qsort [1,2,3];"

		//val in = "list = [1,2,3,4,5]; l = [x*2|x:list|x>1]"

		"if x? | true | false"
		var AST = parser.process(in)
		printer.print(AST)
		AST = stage1.process(AST)
		printer.print(AST)
		AST = stage2.process(AST)
		printer.print(AST)
		AST = stage3.process(AST)
		printer.print(AST)
		AST = stage4.process(AST)
		printer.print(AST)
		val output = codeGen.process(AST)
		println(output)

		//writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		//writeFile("out/std.js", readFile("src/main/scala/std.js"))
		writeFile(outputFile, output)

		/*val f = ("g++ " + outputFile + " -o out/output").!
		println(f)*/


		//val tests = new Tests
		//tests.run()


	}
}