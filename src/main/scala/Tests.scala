import main.{parseString, readFile, writeFile}

class Tests {
	val parser = new Parser
	val printer = new TreePrinter

	val typeChecker = new TypeChecker
	val codeGen = new CodeGenCpp

	var combinedCode = ""

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

	def compile(in: String): String = {
		val ast = parseString(parser, in)
		codeGen.stringiFy(typeChecker.typecheck(ast))
	}

	def basic(): String = {
		val x = "1+1;2*3;5-5;1/2;false & true; 5 & 2; false | false; 2*3 & 5+2/1;"
		compile(x)
		x
	}

	def assign(): String = {
		val x = "a1=1;a2=1+1;a3=a1;a4=println;a5={x};a6=a5 1;a1+=1;a2++;a3*=3;a4=true;"
		compile(x)
		x
	}

	def function(): String = {
		val x = "{};{x};{x=1};{a+b+c};{c;b;a;a+b+c};f1={x*10};"
		compile(x)
		x
	}

	def strings(): String = {
		return ""
	}


	def run() = {
		var s = ""
		s += basic()
		s += assign()
		s += function()
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
