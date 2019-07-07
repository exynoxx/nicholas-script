import scala.util.parsing.combinator.RegexParsers

class Parser extends RegexParsers {
	def word: Parser[Tree] = "\\w+".r ^^ { case s => valueNode(s, "") }

	def number: Parser[Tree] = "\\d+".r ^^ { case s => valueNode(s, "") }

	def op: Parser[Tree] = ("+"|"-"|"*"|"/"|"||"|"&&"|">"|"<"|"<="|">="|"!="|"==") ^^ { case o => opNode(o, "") }

	def binop: Parser[Tree] = (number | word) ~ opt(op ~ binop) ^^ { case n ~ Some(o ~ b) => binopNode(n, b, o, "")
	case n ~ None => n
	}

	def exp: Parser[Tree] = binop

	def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, "") }

	def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }

	def defstatement: Parser[Tree] = "var" ~ word ~ "=" ~ exp ~ ";" ^^
		{ case s1 ~ valueNode(s2,_) ~ s3 ~ t ~ s4 => assignNode(s2, t, "") } |
		word ~ ":=" ~ exp ~ ";" ^^ { case valueNode(s1,_) ~ s2 ~ t ~ s3 => assignNode(s1, t, "") }

	def ifstatement: Parser[Tree] = "if" ~ "(" ~ binop ~ ")" ~ (exp | block) ~ opt("else" ~ (exp | block)) ^^
		{ 	case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), "")
			case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, "")
		}
	def whilestatement: Parser[Tree] = "while" ~ "(" ~ binop ~ ")" ~ (exp | block) ^^ {case s1 ~s2~b~s3~e => whileNode(b,e,"")}

	def statement: Parser[Tree] = ifstatement | whilestatement | defstatement ^^ { case s => s }
}
