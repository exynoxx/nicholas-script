import scala.collection.mutable.ArrayBuffer
import scala.util.parsing.combinator.RegexParsers


class Parser extends RegexParsers {

	/*
	* expression ::= number (("|" | "&" | "^" | "!") number)*
number ::= term ((+|-) term)*
term ::= factor ((*|/) factor)*
factor ::= _ | int | true | false | ( expression ) | block | a.x*/

	// ### BASICS ###
	def word: Parser[Tree] = "\\w+".r ^^ (s => wordNode(s))

	def integer: Parser[Tree] = "\\d+".r ^^ { case s => integerNode(s.toInt) }

	def strings: Parser[Tree] = "\"(?:[^\"\\\\]|\\\\.)*\"".r ^^ (s => stringNode(s)) //TODO: fix "" to much

	def bool: Parser[Tree] = ("true" | "false") ^^ (s => boolNode(s == "true"))


	// ### BINARY OPERATION ###
	def binopList2Tree(left: Tree, list: List[String ~ Tree]): Tree = {
		val (op, right) = list(0) match {
			case string ~ node => (string, node)
		}
		list.size match {
			case 1 => binopNode(op, left, right)
			case _ => binopNode(op, left, binopList2Tree(right, list.drop(1)))
		}
	}


	def binop: Parser[Tree] = number ~ rep(("&" | "|" | "^") ~ number) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)
	}

	def number: Parser[Tree] = term ~ rep(("+" | "-") ~ term) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)

	}

	def term: Parser[Tree] = factor ~ rep(("**" | "//" | "*" | "/") ~ factor) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)
	}

	//(exp)|block | a.x
	def factor: Parser[Tree] = integer | bool | word | block | "(" ~ expression ~ ")" ^^ { case _ ~ x ~ _ => x }


	// ### ASSIGN ###
	def assign: Parser[Tree] = word ~ "=" ~ expression ^^ { case id ~ _ ~ b => assignNode(id, b) }


	// ### ARRAY ###
	def array: Parser[Tree] = "[" ~ opt(expression) ~ rep("," ~ expression) ~ "]" ^^ {
		case _ ~ firstopt ~ list ~ _ =>
			firstopt match {
				case None => arrayNode(List())
				case Some(e) =>
					val l = list.map { case _ ~ t => t }
					arrayNode(e :: l)
			}
	}


	// ### CODE BLOCKS / FUNCTION ###
	def block: Parser[Tree] = "{" ~ rep1(expression ~ (";"|"\n").?) ~ "}" ^^ {
		case _ ~ expList ~ _ => expList match {
			case l => blockNode(l.map { case exp ~ option => exp })
		}
	}

	// ### use f-on-operator ###
	def shortOperatos = ("+" | "*") ^^ (s => wordNode(s))
	def on: Parser[Tree] = (shortOperatos|word) ~ "/" ~ expression ^^ {case id ~ slash ~ exp => binopNode(slash,id,exp)}


	// ### if else ###
	//TODO

	def expression: Parser[Tree] = array | assign | on | binop | block

	/*def assign: Parser[Tree] = defstatement | assignStatement

	def vartype: Parser[Tree] = "[" ~ word ~ "]" ^^ { case _ ~ valueNode(w, ns) ~ _ => valueNode("array(" + w + ")", ns) } |
		"(" ~ opt(word) ~ rep("," ~ word) ~ ")" ~ "=>" ~ word ^^ {
			case _ ~ None ~ l ~ _ ~ _ ~ valueNode(ret, _) => valueNode("()=>" + ret, null)
			case _ ~ Some(valueNode(w1, _)) ~ l ~ _ ~ _ ~ valueNode(ret, _) => {
				val lString = l.map { case s ~ valueNode(e, _) => s + e }.mkString
				valueNode("(" + w1 + lString + ")=>" + ret, null)
			}
		} | word

	def defstatement: Parser[Tree] = "var" ~ word ~ opt(":" ~ vartype) ~ "=" ~ (objectdef | exp | block | ignoreStatement) ~ ";" ^^ {
		case _ ~ valueNode(id, _) ~ Some(_ ~ valueNode(ty, _)) ~ _ ~ t ~ _ => assignNode(valueNode(id, ty), t, true, 0, ty)
		case _ ~ s1 ~ None ~ _ ~ t ~ _ => assignNode(s1, t, true, 0, null)
	} | word ~ ":=" ~ (objectdef | exp | block | ignoreStatement) ~ ";" ^^ { case s1 ~ s2 ~ t ~ s3 => assignNode(s1, t, true, 0, null) }

	def shortdefstatement: Parser[Tree] = word ~ ":=" ~ (objectdef | exp | ignoreStatement) ~ ";" ^^ { case s1 ~ s2 ~ t ~ s3 => assignNode(s1, t, true, 0, null) }

	def assignStatement: Parser[Tree] = (arrayAccess | identifier) ~ "=" ~ (exp | ignoreStatement) ~ ";" ^^ { case id ~ _ ~ b ~ _ => assignNode(id, b, false, 0, null) }

	def incrementStatement: Parser[Tree] = (arrayAccess | identifier) ~ ("+=" | "-=" | "*=" | "/=" | "%=") ~ exp ~ ";" ^^ {
		case id ~ op ~ b ~ _ =>
			val newBody = binopNode(List(id, b), List(opNode(op.charAt(0).toString, null)), 0, null)
			assignNode(id, newBody, false, 0, null)
	}


	// ### ARRAYS ###
	def arrays: Parser[Tree] = arrayrange | arraydef

	def arrayElement: Parser[Tree] = strings | funCall | binop | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s }

	def arraydef: Parser[Tree] = "[" ~ opt(arrayElement) ~ rep("," ~ arrayElement) ~ "]" ^^ {
		case _ ~ firstopt ~ list ~ _ =>
			firstopt match {
				case None => arrayNode(List(), null)
				case Some(e) =>
					val l = list.map { case _ ~ t => t }
					arrayNode(e :: l, null)
			}
	}

	def arrayrangeNumber: Parser[Tree] = integer | identifier | arrayAccess | ("(" ~ binop ~ ")") ^^ { case _ ~ x ~ _ => x }

	def arrayrange: Parser[Tree] = arrayrangeNumber ~ ".." ~ arrayrangeNumber ^^ { case a ~ _ ~ b => rangeNode(a, b, arrayType(intType(null))) }

	def arrayAccess: Parser[Tree] = identifier ~ "[" ~ arrayElement ~ "]" ^^ { case valueNode(name, _) ~ _ ~ b ~ _ => accessNode(name, b, null) }


	// ### FUNCTION ###
	def arg: Parser[Tree] = word ~ ":" ~ vartype ^^ { case valueNode(name, _) ~ s ~ ty => argNode(name, ty) }

	def func: Parser[Tree] = "(" ~ opt(arg) ~ rep("," ~ arg) ~ ")" ~ opt(":" ~ vartype) ~ "=>" ~ (exp | block | ignoreStatement) ^^ {
		case _ ~ Some(arg1) ~ l ~ _ ~ Some(_ ~ ty) ~ _ ~ b =>
			val x = l.map { case s ~ t => t }
			functionNode(null, arg1 :: x, b, functionType(List(), ty))
		case _ ~ Some(arg1) ~ l ~ _ ~ None ~ _ ~ b =>
			val x = l.map { case s ~ t => t }
			functionNode(null, arg1 :: x, b, null)
		case _ ~ None ~ l ~ _ ~ Some(_ ~ ty) ~ _ ~ b =>
			functionNode(null, List(), b, functionType(List(), ty))
		case _ ~ None ~ l ~ _ ~ _ ~ _ ~ b =>
			functionNode(null, List(), b, null)

	}

	// ### FUNCTION CALL ###
	def funArgExp: Parser[Tree] =
		"(" ~ func ~ ")" ^^ { case _ ~ functionNode(_, args, body, ns) ~ _ => anonNode(args, body, ns) } |
			("(" ~ funCall ~ ")" | "(" ~ binop ~ ")") ^^ { case _ ~ x ~ _ => x } |
			strings | arrayAccess | arrays | binop

	def funCall: Parser[Tree] = identifier ~ ":" ~ rep(funArgExp) ^^ { case valueNode(name, _) ~ _ ~ listargs => callNode(name, listargs, false, null) }

	def callStatement: Parser[Tree] = funCall ~ ";" ^^ { case callNode(id, args, _, ns) ~ _ => callNode(id, args, true, ns) }


	// ### CONTROL FLOW ###
	def guard: Parser[Tree] = binop | arrayAccess | funCall

	def ifstatement: Parser[Tree] = "if" ~ "(" ~ guard ~ ")" ~ (statement | block) ~ opt("else" ~ (statement | block)) ^^ {
		case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), null)
		case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, null)
	}

	def whilestatement: Parser[Tree] = "while" ~ "(" ~ guard ~ ")" ~ (statement | block) ^^ { case s1 ~ s2 ~ b ~ s3 ~ e => whileNode(b, e, null) }

	def forstatement: Parser[Tree] = "for" ~ "(" ~ word ~ "in" ~ (arrays | guard) ~ ")" ~ (statement | block) ^^ { case _ ~ _ ~ id ~ _ ~ arr ~ _ ~ body => forNode(id, arr, body, null) }


	// ### OBJECTS ###
	def objectdef: Parser[Tree] = "{" ~ rep(objectrow ~ ",") ~ "}" ^^ { case _ ~ args ~ _ => objectNode(null, args.map(_._1), null) }

	def objectrow: Parser[Tree] =
		word ~ ":" ~ func ^^ { case valueNode(name, _) ~ _ ~ functionNode(_, args, body, ns) => functionNode(name, args, body, ns) } |
			word ~ ":" ~ vartype ^^ { case valueNode(name, _) ~ _ ~ valueNode(ty, _) => objectElementNode(name, ty) } |
			"override" ~ op ~ ":" ~ func ^^ { case _ ~ op ~ _ ~ f => overrideNode(op, f, null) }

	def objectinstansarg: Parser[Tree] = binop | objectinstans | arrays | funCall | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s }

	def objectinstans: Parser[Tree] = identifier ~ "(" ~ opt(objectinstansarg) ~ rep("," ~ objectinstansarg) ~ ")" ^^ {
		case valueNode(name, _) ~ _ ~ None ~ _ ~ _ => objectInstansNode(name, List(), null)
		case valueNode(name, _) ~ _ ~ Some(exp1) ~ list ~ _ => objectInstansNode(name, List(exp1) ++ list.map(_._2), null)
	}

	// ### GENERAL ###
	//exp anything that returns something
	def identifier: Parser[Tree] =
		word ~ rep("." ~ word) ^^ {
			case valueNode(first, _) ~ list => valueNode(first + list.map { case s ~ valueNode(t, _) => s + t }.mkString, null)
		}

	def exp: Parser[Tree] = strings | arrays | objectinstans | func | funCall | binop | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s }

	def block: Parser[Tree] = "{" ~ rep(statement) ~ "}" ^^ { case _ ~ s ~ _ => blockNode(s, null) }

	def retStatement: Parser[Tree] = "return" ~ exp ~ ";" ^^ { case _ ~ e ~ _ => returnNode(e, null) }

	def statement: Parser[Tree] = ifstatement | forstatement | whilestatement | incrementStatement | assign | retStatement | callStatement | ignoreStatement | exp ^^ { case s => s }

	// ### MISC ###
	def ignoreStatement: Parser[Tree] = "\\?\\$[^\\?]*\\?\\$".r ^^ { case s => lineNode(s.substring(2, s.length - 2), null) }

	//def inlineComment: Parser[Tree] = "#^[;]*".r ^^ { case s => lineNode("//"+s.substring(1, s.length - 1), "") }

	// ### PARSING START HERE ###
	def start: Parser[Tree] = rep(statement) ^^ { case s => blockNode(s, "") }*/
}
