import scala.Option
import scala.io.Source
import scala.util.parsing.combinator._

class Parser extends RegexParsers {
	def word: Parser[Tree] = """[a-z]+""".r ^^ { case s => valueNode(s, "") }

	def number: Parser[Tree] = """\d+""".r ^^ { case s => valueNode(s, "") }

	def op: Parser[Tree] = """([+/-\\*<>=!]|<=|>=|\|\||&&)""".r ^^ { case o => opNode(o, "") }

	def binop: Parser[Tree] = (number | word) ~ opt(op ~ binop) ^^ { case n ~ Some(o ~ b) => binopNode(n, b, o, "")
	case n ~ None => n
	}

	def exp: Parser[Tree] = binop

	def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, "") }

	def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }

	def defstatement: Parser[Tree] = "var" ~ "[a-z]+".r ~ "=" ~ exp ^^ { case s1 ~ s2 ~ s3 ~ t => assignNode(s2, t, "") } | "[a-z]+".r ~ ":=" ~ exp ^^ { case s1 ~ s2 ~ t => assignNode(s1, t, "") }


	def ifstatement: Parser[Tree] =
		"if" ~ "(" ~ binop ~ ")" ~ (exp|block) ~ opt("else" ~ (exp|block)) ^^ { case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), "")
		case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, "")
		}

	def statement: Parser[Tree] = ifstatement | defstatement ^^ { case s => s }
}

object main {

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val alltext = bufferedSource.getLines().mkString
		bufferedSource.close
		alltext
	}

	def main(args: Array[String]): Unit = {
		val p = new Parser
		val in = readFile("src/main/scala/test.ns")
		p.parse(p.start, in) match {
			case p.Success(matched, _) => println(matched)
			case p.Failure(msg, _) => println(s"FAILURE: $msg")
			case _ => println(s"ERROR: ")
		}
	}
}