import scala.util.parsing.combinator.RegexParsers

class Parser extends RegexParsers {
    def word: Parser[Tree] = "\\w+".r ^^ { case s => valueNode(s, "") }

    def number: Parser[Tree] = "\\d+".r ^^ { case s => valueNode(s, "") }

    def op: Parser[Tree] = ("+" | "-" | "*" | "/" | "||" | "&&" | ">" | "<" | "<=" | ">=" | "!=" | "==") ^^ { case o => opNode(o, "") }

    def binop: Parser[Tree] = (number | word) ~ opt(op ~ binop) ^^ { case n ~ Some(o ~ b) => binopNode(n, b, o, "")
    case n ~ None => n
    }

    def exp: Parser[Tree] = funCall | binop | "(" ~ funCall ~ ")" ^^ {case _ ~ s ~ _ => s} | "(" ~ binop ~ ")" ^^ {case _ ~ s ~ _ => s}

    def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, "") }

    def assign: Parser[Tree] = defstatement | assignStatement

    def defstatement: Parser[Tree] = "var" ~ word ~ "=" ~ (exp | func) ~ ";" ^^ { case s1 ~ valueNode(s2, _) ~ s3 ~ t ~ s4 => assignNode(s2, t, true, "") } |
        word ~ ":=" ~ (exp | func) ~ ";" ^^ { case valueNode(s1, _) ~ s2 ~ t ~ s3 => assignNode(s1, t, true, "") }

    def assignStatement: Parser[Tree] = word ~ "=" ~ (exp | func) ~ ";" ^^ { case valueNode(s1, _) ~ s2 ~ t ~ s3 => assignNode(s1, t, false, "") }

    def retStatement: Parser[Tree] = "return" ~ exp ^^ { case _ ~ e => returnNode(e, "") }

    def func: Parser[Tree] = func0 | func1 | funcn

    def func0: Parser[Tree] = "()" ~ "=>" ~ (exp | block) ^^ { case s1 ~ s2 ~ b => functionNode("", List(), b, "") }

    def func1: Parser[Tree] = "(" ~ arg ~ ")" ~ "=>" ~ (exp | block) ^^ { case s1 ~ l ~ s3 ~ s4 ~ b => functionNode("", List(l), b, "") }

    def funcn: Parser[Tree] = "(" ~ arg ~ rep("," ~ arg) ~ ")" ~ "=>" ~ (exp | block) ^^ { case s1 ~ arg1 ~ (l: List[String ~ Tree]) ~ s2 ~ s3 ~ b =>
        val x = l.map { case s ~ t => t }
        functionNode("", arg1 :: x, b, "")
    }

    def funCall: Parser[Tree] = word ~ ":" ~ rep(exp) ^^ {case valueNode(name,_) ~ _ ~ listargs => callNode(name,listargs,false,"")}
    def arg: Parser[Tree] = word ~ ":" ~ word ^^ { case valueNode(name, _) ~ s ~ valueNode(ty, _) => argNode(name, ty) }

    def ifstatement: Parser[Tree] = "if" ~ "(" ~ binop ~ ")" ~ (exp | block) ~ opt("else" ~ (exp | block)) ^^ { case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), "")
    case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, "")
    }

    def whilestatement: Parser[Tree] = "while" ~ "(" ~ binop ~ ")" ~ (exp | block) ^^ { case s1 ~ s2 ~ b ~ s3 ~ e => whileNode(b, e, "") }

    def statement: Parser[Tree] = ifstatement | whilestatement | assign | retStatement ^^ { case s => s }

    def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }
}
