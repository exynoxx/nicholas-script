import scala.io.Source

object mainObj extends App {

	def printArray(x: Array[String]) = {
		val str = x.foldLeft("") { (a: String, b: String) => a + b + " " }
		println(str)
	}

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val ret = bufferedSource.getLines().mkString
		bufferedSource.close
		ret
	}


	val l = new lexer;
	val p = new parser(false);

	val rules = readFile("src/main/scala/grammer.g").split("[$\n]")
	rules.foreach(s => p.addRule(s))
	/*
	func ::= LPAREN args RPAREN IMPL fbody
	args ::= (STRING(*))*...............TODO
	fbody ::= binop | if | while | block
	 */

	val input = readFile("src/main/scala/test.ns")
	val parseTree: Tree = p.parse(l.tokenize(input))
	print(parseTree)

}

