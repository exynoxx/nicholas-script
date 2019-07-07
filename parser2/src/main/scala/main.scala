import scala.io.Source
import scala.util.parsing.combinator._
class TreePrinter {
	def printMinus(sym: String, depth: Int): String = {
		val spaces = (0 to depth).map(x => sym).mkString("")
		spaces
	}

	def recursion(t: Tree, depth: Int = 0, increment: Int = 6): String = {
		t match {
			case valueNode(value, ns) => printMinus("-", depth) +
				"valueNode(" + value + "," + ns + ")\n"
			case binopNode(l, r, o, ns) => printMinus("-", depth) +
				"binopNode(" + recursion(o) + ")\n" +
				recursion(l, depth + increment) +
				recursion(r, depth + increment)
			case opNode(b, ns) => b
			case assignNode(id, b, ns) => printMinus("-", depth) +
				"assignNode(" + id + ")\n" +
				recursion(b, depth + increment)
			case blockNode(children, ns) => {
				val s = printMinus("-", depth) + "blockNode(" + children.length + ")\n"
				val pc = children.map(x => recursion(x, depth + increment)).mkString("")
				s + pc
			}
			case ifNode(c, b, Some(e), ns) => printMinus("-", depth) +
				"ifNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment) +
				recursion(e, depth + increment)
			case ifNode(c, b, None, ns) => printMinus("-", depth) +
				"ifNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
			case whileNode(c,b,ns) => printMinus("-", depth) +
				"whileNode\n" +
				recursion(c, depth + increment) +
				recursion(b, depth + increment)
		}
	}

	def print(t: Tree): Unit = {
		println(t)
		println(recursion(t, 1))
	}
}

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

object main {

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val alltext = bufferedSource.getLines().mkString
		bufferedSource.close
		alltext
	}

	def main(args: Array[String]): Unit = {
		val p = new Parser
		val printer = new TreePrinter
		val in = readFile("src/main/scala/test.ns")
		p.parse(p.start,in) match {
			case p.Success(t, _) => printer.print(t)
			case p.Failure(msg1,msg2) => println(s"Error: $msg1, $msg2")
			case p.Error(msg1,msg2) => println(s"Error: $msg1, $msg2")
		}

	}
}