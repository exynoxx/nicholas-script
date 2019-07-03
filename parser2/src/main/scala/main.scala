object mainObj extends App {



	val l = new lexer;
	val p = new parser;
	p.addRule("statement::=if|while|assign SEMICOLON")
	p.addRule("assign::=def|defshort")
	p.addRule("def::=VAR STRING(*) EQ binop|VAR STRING(*) EQ func")
	p.addRule("defshort::=STRING(*) DEFEQ binop|STRING(*) DEFEQ func")
	p.addRule("binop::=value|value OP binop")
	p.addRule("OP ::= PLUS | MINUS | MULT | DIV | AND | OR | GE | LE | GEQ | LEQ | NEQ")
	p.addRule("value::=INT(*)")
	p.rules.foreach{case (k,v) => println(k.foldLeft("") { (a: String, b: String) => a + b + " " } + " -> " + v)}

	val input = "var   a =  5+3*2+1;"
	//printt(input)
	val tokens = l.tokenize(input)
	p.parse(tokens)
	val str = p.transitionList.foldLeft("") { (a: String, b: String) => a + b + " " }
	println(str)


}

