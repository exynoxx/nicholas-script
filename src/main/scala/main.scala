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
		val codeGen = new CodeGenCpp

		//val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.cpp"

		//val in = "even = {(x%2==0)?true:false};println (even 5);println (even 6);println (even 7);"
		//val in = "y=1+1;f={k*2+l};x=\"str\";x=y;non=f x 1;y=5*x;x=y;print x;"
		//val in = "fib = { if (n <= 1) | 1 | (fib n-1) + (fib n-2)}; x = fib 40;"

		//val in = "one = [1]*10; a = [1,2,3]; [1,2,3]; b=a$0;"
		//val in = "f={x}; a = f 1; b = {x*2}; f = a; b = f + 10; 0"
		//val in = "one = [1]*10; two = {x*2}; [1,2,3] two; [1,2,3] {x-1}; a = one$0; 1 + [1,1,1]; [2,2,2]+2"

		//anon func val in ="println ({ (n <= 1) ? 1 : (fib n-1) + (fib n-2)} 50);0"

		//val in = "[1,1,1] {x*10} |> println;0"

		//val in = "qsort = {[x$0]}; (qsort [1,2,3]) |> println"
		//val in = "qsort = {}; (qsort [1,2,3]) |> println"

		//val in = "qsort = { (qsort [x|x:list|x<list$0]) + [list$0] + (qsort [x|x:list|x>=list$0])}; [5,2,3,6,1,4] |> qsort |> println"
		//val in = "qsort = { (qsort [x|x:list|x<list$0]) ++ [list$0] ++ (qsort [x|x:list|x>=list$0]) }; qsort [1,2,3];"
		//val in = "qsort = { if list?<=1 | list | (qsort [x|x:list|x<list$0]) ++ [list$0] ++ (qsort [x|x:list|x>list$0]) }; qsort [2,6,20,11,45,6,33,1,7,8];"

		//val in = "x=[1,2,3]; f2={x+y}; y=f2 4"

		//val in = "qsort = { if list?<=1 | list | (qsort [x|x:list|x<list$0]) ++ [list$0] ++ (qsort [x|x:list|x>list$0]) }; qsort [2,6,20,11,45,6,33,1,7,8];"


		//TODO: have local variables overlapping with outside variables in list comprehension
		//TODO: make array inside list comprehension, support
		//val in = "x=[1,2,3]; x = [\"a\", \"b\"]; a = x+1; y = [\"1\", \"2\"]; I={x}; intArray = y I; b = 1; [b*2+z|z:x]; [1,2,3] {k*b};"


		//compiles
		val basic = "a=1;b=true;c=[1,2,3]; d = a + 1; e = a + \"s\";"

		//compiles
		val arrays = "a=[1,2,3]; b=5; c=a+b; cc = a + 6; d=[\"a\",\"b\",\"c\"]; e = d$0; f = [f1=1,f2=2,f3=3]; g1=f$0; g2=f1; h1 = [1]*10; h2=[1,2]*10;"

		//compiles
		val strings = "f1 = \"hello\"; f2 = \"world\"; f3 = f1+f2; f4 = f3 + \" !\";"

		//compiles
		val functions = "f1={x}; a = true; f2 = {if (a==true)|true|a;};f1 1; f2 true;"

		val chain = "double = {x*2}; y = [1,2,3]; y |> double |> print;"

		//compiles
		val toInt = "a = [\"1\",\"2\",\"3\"]; b=a {I x};c = a$0;"

		//compiles
		//val split = "in = \"this;is;an;array\"; inArr = split in \";\"; inInt = inArr {I x}"
		val split = "in = \"this;is;an;array\"; inInt = (split in \";\") {I x};"

		//val in = basic + arrays + strings + functions + chain + toInt + split

		val in = "even = [x*2|x:1..100|x%2==0]"


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

		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		//writeFile("out/std.js", readFile("src/main/scala/std.js"))
		writeFile(outputFile, output)

		/*val f = ("g++ " + outputFile + " -o out/output").!
		println(f)*/


		//val tests = new Tests
		//tests.run()


	}
}