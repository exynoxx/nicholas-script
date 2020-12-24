import scala.util.parsing.combinator.RegexParsers


class Parser extends RegexParsers {
	def word: Parser[Tree] = "\\w+".r ^^ { case s => valueNode(s, null) }

	def number: Parser[Tree] = "\\d+".r ^^ { case s => valueNode(s, "int") }

	def strings: Parser[Tree] = "\"(?:[^\"\\\\]|\\\\.)*\"".r ^^ { case s => valueNode(s, "actualstring") }

	def op: Parser[Tree] = intOp | boolOp

	def boolOp: Parser[Tree] = ("||" | "&&" | "<=" | ">=" | ">" | "<" | "!=" | "==") ^^ { case o => opNode(o, "bool") }

	def intOp: Parser[Tree] = ("+" | "-" | "**" | "*" | "/" | "%") ^^ { case o => opNode(o, "int") }

	def binop: Parser[Tree] = (number | arrayAccess | word | strings | "(" ~ binop ~ ")" ^^ { case _ ~ b ~ _ => b }) ~ rep(op ~ (number | arrayAccess | word | strings | "(" ~ binop ~ ")" ^^ { case _ ~ b ~ _ => b })) ^^ {
		case n ~ List() => n
		case n ~ (o: List[Tree ~ Tree]) =>
			val nn = o.map { case _ ~ num => num }
			val oo = o.map { case op ~ _ => op }
			binopNode(n :: nn, oo, 0, null)
	}

	def arrayAccess: Parser[Tree] = word ~ "[" ~ binop ~ "]" ^^ { case valueNode(name, _) ~ _ ~ b ~ _ => accessNode(name, b, null) }

	def exp: Parser[Tree] = propertyCall | funCall | binop | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s }

	def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, null) }

	def assign: Parser[Tree] = defstatement | assignStatement

	def defstatement: Parser[Tree] = "var" ~ word ~ opt(":" ~ word) ~ "=" ~ (arrays | exp | func) ~ ";" ^^ {
		case _ ~ valueNode(id, _) ~ Some(_ ~ valueNode(ty, _)) ~ _ ~ t ~ _ => assignNode(id, t, true, 0, ty)
		case _ ~ valueNode(id, _) ~ None ~ _ ~ t ~ _ => assignNode(id, t, true, 0, null)

	} | word ~ ":=" ~ (arrays | exp | func) ~ ";" ^^ { case valueNode(s1, _) ~ s2 ~ t ~ s3 => assignNode(s1, t, true, 0, null) }

	def assignStatement: Parser[Tree] = word ~ "=" ~ (arrays | exp | func) ~ ";" ^^ { case valueNode(id, _) ~ _ ~ b ~ _ => assignNode(id, b, false, 0, null) }

	def incrementStatement: Parser[Tree] = word ~ ("+=" | "-=" | "*=" | "/=" | "%=") ~ exp ~ ";" ^^ {
		case valueNode(id, _) ~ op ~ b ~ _ =>
			val newBody = binopNode(List(valueNode(id, null), b), List(opNode(op.charAt(0).toString,null)), 0, null)
			assignNode(id, newBody, false, 0, null)
	}

	def arrays: Parser[Tree] = arraydef | arrayrange

	def arraydef: Parser[Tree] = "[" ~ opt(exp) ~ rep("," ~ exp) ~ "]" ^^ { case _ ~ firstopt ~ list ~ _ =>
		firstopt match {
			case None => arrayNode(null, null)
			case Some(e) =>
				val l = list.map { case _ ~ t => t }
				arrayNode(e :: l, null)
		}
	}

	def arrayrangeNumber: Parser[Tree] = number | ("(" ~ binop ~ ")" ^^ { case _ ~ x ~ _ => x })

	def arrayrange: Parser[Tree] = arrayrangeNumber ~ ".." ~ arrayrangeNumber ^^ { case a ~ _ ~ b => rangeNode(a, b, "array(int)") }

	def retStatement: Parser[Tree] = "return" ~ exp ~ ";" ^^ { case _ ~ e ~ _ => returnNode(e, "") }

	def func: Parser[Tree] = "(" ~ opt(arg) ~ rep("," ~ arg) ~ ")" ~ opt(":" ~ word) ~ "=>" ~ (exp | block | ignoreStatement) ^^ {
		case _ ~ Some(arg1) ~ (l: List[String ~ Tree]) ~ _ ~ Some(_ ~ valueNode(ty, _)) ~ _ ~ b =>
			val x = l.map { case s ~ t => t }
			functionNode("", arg1 :: x, b, ty)
		case _ ~ None ~ l ~ _ ~ Some(_ ~ valueNode(ty, _)) ~ _ ~ b =>
			functionNode("", List(), b, ty)
		case _ ~ Some(arg1) ~ (l: List[String ~ Tree]) ~ _ ~ None ~ _ ~ b =>
			val x = l.map { case s ~ t => t }
			functionNode("", arg1 :: x, b, null)
		case _ ~ None ~ l ~ _ ~ None ~ _ ~ b =>
			functionNode("", List(), b, null)
	}

	def funArgExp: Parser[Tree] = propertyCall | ("(" ~ propertyCall ~ ")" | "(" ~ funCall ~ ")" | "(" ~ binop ~ ")") ^^ { case _ ~ x ~ _ => x } | binop

	def funCall: Parser[Tree] = word ~ ":" ~ rep(funArgExp) ^^ { case valueNode(name, _) ~ _ ~ listargs => callNode(name, listargs, false, null) }

	def propertyCall: Parser[Tree] = propertyCallFunCall | propertyCallBinop

	def propertyCallBinop: Parser[Tree] = binop ~ "." ~ word ^^ { case arg1 ~ _ ~ valueNode(id, _) => callNode(id, List(arg1), false, null) }

	def propertyCallFunCall: Parser[Tree] = "(" ~ funCall ~ ")" ~ "." ~ word ^^ { case _ ~ arg1 ~ _ ~ _ ~ valueNode(id, _) => callNode(id, List(arg1), false, null) }

	def callStatement: Parser[Tree] = propertyCall | funCall ~ ";" ^^ { case callNode(id, args, _, ns) ~ _ => callNode(id, args, true, ns) }

	def arg: Parser[Tree] = word ~ ":" ~ word ^^ { case valueNode(name, _) ~ s ~ valueNode(ty, _) => argNode(name, ty) }

	def ifstatement: Parser[Tree] = "if" ~ "(" ~ binop ~ ")" ~ (exp | block) ~ opt("else" ~ (exp | block)) ^^ { case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), null)
	case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, null)
	}

	def whilestatement: Parser[Tree] = "while" ~ "(" ~ binop ~ ")" ~ (exp | block) ^^ { case s1 ~ s2 ~ b ~ s3 ~ e => whileNode(b, e, null) }


	def ignoreStatement: Parser[Tree] = "\\?\\$[^\\?]*\\?\\$".r ^^ { case s => lineNode(s.substring(2, s.length - 2), "") }

	def statement: Parser[Tree] = callStatement | ifstatement | whilestatement | incrementStatement | assign | retStatement | ignoreStatement ^^ { case s => s }

	def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }
}
