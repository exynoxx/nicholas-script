object mainObj extends App {

	def printt(s: String): Unit = {
		print(l.tokenize(s).map(s => print(s + " ")))
		println("")
	}

	val l = new lexer;
	val p = new parser;
	p.addRule("statement::=if|while|assign SEMICOLON")
	p.addRule("assign::=def|defshort")
	p.addRule("def::=VAR STRING(*) EQ assignval")
	p.addRule("defshort::=STRING(*) DEFEQ assignval")
	p.addRule("assignval::=binop|func")
	p.addRule("binop::=value|value OP value")
	p.addRule("value::=INT(*)")

	val input = "var   a =     5;"
	//printt(input)
	val tokens = l.tokenize(input)
	p.parse(tokens)
}

