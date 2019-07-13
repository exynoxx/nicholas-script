import scala.util.parsing.combinator.RegexParsers

class Parser extends RegexParsers {
    def word: Parser[Tree] = "\\w+".r ^^ { case s => valueNode(s, null) }

    def number: Parser[Tree] = "\\d+".r ^^ { case s => valueNode(s, "int") }

    def strings: Parser[Tree] = "\"(?:[^\"\\\\]|\\\\.)*\"".r ^^ { case s => valueNode(s, "string") }

    def op: Parser[Tree] = ("+" | "-" | "*" | "/" | "||" | "&&" | ">" | "<" | "<=" | ">=" | "!=" | "==") ^^ { case o => opNode(o, "") }

    def binop: Parser[Tree] = (number | word| strings) ~ opt(op ~ binop) ^^ { case n ~ Some(o ~ b) => binopNode(n, b, o, "")
    case n ~ None => n
    }

    //TODO: funcall in args
    def exp: Parser[Tree] = funCall | binop | /*"(" ~ funCall ~ ")" ^^ { case _ ~ s ~ _ => s } |*/ "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s }

    def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, "") }

    def assign: Parser[Tree] = defstatement | assignStatement

    def defstatement: Parser[Tree] = "var" ~ word ~ opt(":" ~ word) ~ "=" ~ (exp | func) ~ ";" ^^
        {
            case _ ~ valueNode(id, _) ~ Some(_~valueNode(ty, _)) ~ _ ~ t ~ _ => assignNode(id, t, true, ty)
            case _ ~ valueNode(id, _) ~ None ~ _ ~ t ~ _ => assignNode(id, t, true, null)

        } |
        word ~ ":=" ~ (exp | func) ~ ";" ^^ { case valueNode(s1, _) ~ s2 ~ t ~ s3 => assignNode(s1, t, true, null) }

    def assignStatement: Parser[Tree] = word ~ "=" ~ (exp | func) ~ ";" ^^ { case valueNode(s1, _) ~ s2 ~ t ~ s3 => assignNode(s1, t, false, "") }

    def retStatement: Parser[Tree] = "return" ~ exp ~ ";" ^^ { case _ ~ e ~ _ => returnNode(e, "") }


    def func: Parser[Tree] = "(" ~ opt(arg) ~ rep("," ~ arg) ~ ")" ~ opt(":" ~ word) ~ "=>" ~ (exp | block) ^^
        {
            case _ ~ Some(arg1) ~ (l: List[String ~ Tree]) ~ _ ~Some(_ ~ valueNode(ty, _)) ~ _ ~ b =>
                val x = l.map { case s ~ t => t }
                functionNode("", arg1 :: x, b, ty)
            case _ ~ None ~ l ~ _ ~Some(_ ~ valueNode(ty, _)) ~ _ ~ b =>
                functionNode("", List(), b, ty)
            case _ ~ Some(arg1) ~ (l: List[String ~ Tree]) ~ _ ~None ~ _ ~ b =>
                val x = l.map { case s ~ t => t }
                functionNode("", arg1 :: x, b, null)
            case _ ~ None ~ l ~ _ ~ None ~ _ ~ b =>
                functionNode("", List(), b, null)
    }

    def funCall: Parser[Tree] = word ~ ":" ~ rep(binop) ^^ { case valueNode(name, _) ~ _ ~ listargs => callNode(name, listargs, false, "") }

    def arg: Parser[Tree] = word ~ ":" ~ word ^^ { case valueNode(name, _) ~ s ~ valueNode(ty, _) => argNode(name, ty) }

    def ifstatement: Parser[Tree] = "if" ~ "(" ~ binop ~ ")" ~ (exp | block) ~ opt("else" ~ (exp | block)) ^^ { case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), "")
    case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, "")
    }

    def whilestatement: Parser[Tree] = "while" ~ "(" ~ binop ~ ")" ~ (exp | block) ^^ { case s1 ~ s2 ~ b ~ s3 ~ e => whileNode(b, e, "") }

    def statement: Parser[Tree] = ifstatement | whilestatement | assign | retStatement ^^ { case s => s }

    def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }
}
