import Util.{readFile, writeFile}

import sys.process._

class Tests {
	val parser = new Parser
	val printer = new TreePrinter

	val typeChecker = new TreeAugmenter
	val codeGen = new CodeGenCpp

	var combinedCode = ""

	def compile(in: String): String = codeGen.process(typeChecker.process(parser.process(in)))

	def basic(): String = {
		//val x = "1+1;2*3;5-5;1/2;false & true; 5 & 2; false | false; 2*3 & 5+2/1;"
		val x = "1+1;1+2*3;5-5;1/2; b = 2*(3+1) + 5+2/1 - 10;"
		compile(x)
		x
	}

	def bools(): String = {
		val x = "true"
		compile(x)
		x
	}

	def assign(): String = {
		val x = "a1=1;a2=1+1;a3=a1;a4=println;a5={x};a6=a5 1;a=a6;" //a1+=1;a2++;a3*=3;a4=true;a7=a8=a9=3;
		compile(x)
		x
	}

	//TODO: access
	def arrays(): String = {
		val x = "[]"
		compile(x)
		x
	}

	def function(): String = {
		val x = "{};{x};{x=1};{a*b+c};{c;b;a;a*b+c};f1={x*10};"
		compile(x)
		x
	}

	def strings(): String = {
		return ""
	}

	def ifs(): String = {
		return ""
	}

	def combine():String = {
		val x = "result = b + a + f1 b;println result"
		x
	}


	def run() = {
		var s = ""
		s += basic()
		s += assign()
		s += function()
		s += combine()

		writeFile("out/std.cpp", readFile("src/main/scala/std.cpp"))
		writeFile("out/output.cpp", compile(s))
		val f = ("g++ out/output.cpp -o out/output").!
		println(f)


	}

}

//val in = "1+2*3 && (5/2) * (1+2)"
//val in = "a=[1,2^3,1+2*3 & 5/2];b=1+1"
//val in = "[];[1];[1,2,3]"
//val in = "a={1+1}*5;4*{1+1};c=[abc=1,2,3,4]"
//val in = "first=+/[1,2,b=3,+/c];c=1+2;([1])*10"
//val in = "{1+1}/a;+/a;f/a; {1} 1 1"
//val in = "a;f;f/a; {f} 1 1;{x}/[1,2,3];f 1 1 + 1; + 1 a 3 4"
//val in = "a=[1,2,b=3];c=a$2;[0]$0; d=[e=[1,2],[3,4]]$0"
//val in = "+/l10+2; !10; !true; 1+!10; ({x+5}[1,2,3]);"
//val in = "print=_NS_print;six = {a+b*c} 1 2 3; false = !true; fac = !5; list = {x*2}/[1,2,3]; a = ([1]*10)+1; print a"
//val in = "println=_NS_println; mult2 = {x*2}; x = !false; println x; x = mult2 5; println x; x = mult2 [1,1]; println x; x = x+1; println x"
//val in = "x=5;x=[1,\"2\",3];x=1+1;"
