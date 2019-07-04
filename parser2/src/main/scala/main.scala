import scala.io.Source

object mainObj extends App {

	def printArray(x: Array[String]) = {
		val str = p.transitionList.foldLeft("") { (a: String, b: String) => a + b + " " }
		println(str)
	}

	def readFile(filename: String): Array[String] = {
		val bufferedSource = Source.fromFile(filename)
		val allrules = bufferedSource.getLines().mkString
		bufferedSource.close
		allrules.split("[$\n]")
	}


	val l = new lexer;
	val p = new parser;

	val rules = readFile("src/main/scala/grammer.g")
	rules.foreach(s => p.addRule(s))
	/*
	p.addRule("statement::=if|while|assign SEMICOLON")
	p.addRule("assign::=def|defshort")
	p.addRule("def::=VAR STRING(*) EQ binop|VAR STRING(*) EQ func")
	p.addRule("defshort::=STRING(*) DEFEQ binop|STRING(*) DEFEQ func")
	p.addRule("binop::=value|value op binop")
	p.addRule("op ::= PLUS | MINUS | MULT | DIV | AND | OR | GE | LE | GEQ | LEQ | NEQ")
	p.addRule("value::=INT(*)")

	func ::= LPAREN args RPAREN IMPL fbody
	args ::= (STRING(*))*...............TODO
	fbody ::= binop | if | while | block
	 */
	p.rules.foreach { case (k, v) => println(k.foldLeft("") { (a: String, b: String) => a + b + " " } + " -> " + v) }

	val input = "b := 5+5;"
	val parseTree: Tree = p.parse(l.tokenize(input))
	print(parseTree)

}

