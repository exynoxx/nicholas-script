class lexer {

	def tokenize(input: String): Array[String] = {
		var currentToken = ""
		var stream = ""
		var i = 0

		while (i < input.length) {

			if ("[ \n]".r.matches(input.charAt(i).toString)) {
				i += 1;
			}

			else if (i < input.length && "[a-zA-Z]".r.matches(input.charAt(i).toString)) {
				while ("\\w".r.matches(input.charAt(i).toString)) {
					currentToken += input.charAt(i)
					i += 1
				}
				stream += "STRING(" + currentToken + ") "
				currentToken = ""
			}

			else if ("\\d".r.matches(input.charAt(i).toString)) {
				while (i < input.length && "\\d".r.matches(input.charAt(i).toString)) {
					currentToken += input.charAt(i)
					i += 1
				}
				stream += "INT(" + currentToken + ") "
				currentToken = ""
			}

			else if (input.charAt(i) == '{') {
				stream += "LBRACE "
				i += 1
			}
			else if (input.charAt(i) == '}') {
				stream += "RBRACE "
				i += 1
			}
			else if (input.charAt(i) == '[') {
				stream += "LBRACK "
				i += 1
			}
			else if (input.charAt(i) == ']') {
				stream += "RBRACK "
				i += 1
			}
			else if (input.charAt(i) == '(') {
				stream += "LPAREN "
				i += 1
			}
			else if (input.charAt(i) == ')') {
				stream += "RPAREN "
				i += 1
			}

			else if (input.charAt(i) == ':' && input.charAt(i + 1) == '=') {
				stream += "DEFEQ "
				i += 2
			}

			else if (input.charAt(i) == ':') {
				stream += "COLON "
				i += 1
			}
			else if (input.charAt(i) == ';') {
				stream += "SEMICOLON "
				i += 1
			}
			else if (input.charAt(i) == '+') {
				stream += "PLUS "
				i += 1
			}
			else if (input.charAt(i) == '-') {
				stream += "MINUS "
				i += 1
			}
			else if (input.charAt(i) == '*') {
				stream += "MULT "
				i += 1
			}
			else if (input.charAt(i) == '/') {
				stream += "DIV "
				i += 1
			}
			else if (input.charAt(i) == '<' && input.charAt(i + 1) == '=') {
				stream += "LEQ "
				i += 2

			}
			else if (input.charAt(i) == '>' && input.charAt(i + 1) == '=') {
				stream += "GEQ "
				i += 2
			}
			else if (input.charAt(i) == '=' && input.charAt(i + 1) == '>') {
				stream += "IMPL "
				i += 2
			}
			else if (input.charAt(i) == '<') {
				stream += "LE "
				i += 1
			}
			else if (input.charAt(i) == '>') {
				stream += "GE "
				i += 1
			}

			else if (input.charAt(i) == '!') {
				stream += "NOT "
				i += 1
			}
			else if (input.charAt(i) == '!' && input.charAt(i + 1) == '=') {
				stream += "NOTEQ "
				i += 2
			}
			else if (input.charAt(i) == '|' && input.charAt(i + 1) == '|') {
				stream += "OR "
				i += 2
			}
			else if (input.charAt(i) == '&' && input.charAt(i + 1) == '&') {
				stream += "AND "
				i += 2
			}
			else if (input.charAt(i) == '.') {
				stream += "DOT "
				i += 1
			}
			else if (input.charAt(i) == ',') {
				stream += "COMMA "
				i += 1
			}
			else if (input.charAt(i) == '=') {
				stream += "EQ "
				i += 1
			} else {
				println("error")
				println(input.charAt(i))
				i += 1
			}

		}
		stream = stream.replace("STRING(while)", "WHILE")
		stream = stream.replace("STRING(if)", "IF")
		stream = stream.replace("STRING(var)", "VAR")
		return stream.split(" ")
	}

}
