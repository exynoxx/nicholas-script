import scala.collection.mutable.ArrayBuffer
import scala.util.parsing.combinator.RegexParsers


class Parser extends RegexParsers {

	def debug(x:Parser[Tree])(out:String):Parser[Tree] = {
		val shouldLog = false

		if (shouldLog){
			log(x)(out)
		} else {
			x
		}
	}

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

	val boolOp = "&&" | "||" | "&" | "|" | "^" | ">" | ">="  | "<=" | "<" | "==" | "!="


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


	def binop: Parser[Tree] = debug(number ~ rep(boolOp ~ number) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)
	})("binop")

	def number: Parser[Tree] = debug(term ~ rep(("+" | "-") ~ term) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)

	})("number")

	def term: Parser[Tree] = debug(factor ~ rep(("**" | "^" | "*" | "/" | "%") ~ factor) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)
	})("term")

	/*def boolean: Parser[Tree] = debug(factor ~ rep(("&&" | "&" | "||" | "|" | "!=" | "==" | ">=" | "<=" | ">" | "<") ~ factor) ^^ {
		case n ~ List() => n
		case n ~ (l: List[String ~ Tree]) => binopList2Tree(n, l)
	})("term")*/

	//(exp)|block | a.x
	def factor: Parser[Tree] = debug(access | integer | strings | bool | word | call  | unary | block | array | "(" ~ expression ~ ")" ^^ { case _ ~ x ~ _ => x })("factor")


	// ### ASSIGN ###
	//TODO: array access assign
	def assign: Parser[Tree] = debug(word ~ "=" ~ expression ^^ { case id ~ _ ~ b => assignNode(id, b) })("assign")


	// ### ARRAY ###
	def array: Parser[Tree] = debug("[" ~ repsep(expression, ",") ~ "]" ^^ {
		case _ ~ list ~ _ => arrayNode(list)
	})("array")

	def access: Parser[Tree] = debug((word | array) ~ "$" ~ integer ^^ { case array ~ _ ~ idx => accessNode(array, idx) })("access")


	// ### CODE BLOCKS ###
	def block: Parser[Tree] = debug("{" ~ repsep(expression, ";" | "\n") ~ (";" | "\n").? ~ "}" ^^ {
		case _ ~ expList ~ _ ~ _ => blockNode(expList)
	})("block")

	// ### FUNCTION CALL ###
	//TODO: (exp)
	def call: Parser[Tree] = debug((word | block) ~ rep1(array | binop) ^^ { case f ~ args => callNode(f, args) })("call")


	// ### use f-on-operator ###
	//def shortOperatos = ("+" | "*") ^^ (s => wordNode(s))


	// ### if else ###
	def ifStatement: Parser[Tree] = debug(
		"(" ~ expression ~ ")" ~ "?" ~ expression ~ (":" ~ expression).? ^^ {
			case _ ~ cond ~ _ ~ _ ~ body ~ None => ifNode(cond,body,None)
			case _ ~ cond ~ _ ~ _ ~ body ~ Some(_ ~ elsBody) => ifNode(cond,body,Some(elsBody))
		}
	)("if")
	//def ifCondition: Parser[Tree] = expression | ("(" ~ expression ~ ")" ^^ { case _ ~ x ~ _ => x })

	def notOpeator: Parser[Tree] = debug("!" ~ expression ^^ { case not ~ exp => unopNode(not, exp) })("notOpeator")

	def unary: Parser[Tree] = debug(notOpeator)("unary")

	def expression: Parser[Tree] = debug(ifStatement  | assign | call | binop | access | unary | array | block | "(" ~ expression ~ ")" ^^ { case _ ~ x ~ _ => x })("exp")

	def process(s: String): Tree = {
		println("---------------------- parsing ----------------------")
		val inn = "{" + s + "}"
		this.parse(expression,inn) match {
			case this.Success(t, _) =>
				println("success")
				t
			case x => println("Error in .parse: " + x)
				nullLeaf()
		}
	}



	/*def assign: Parser[Tree] = debug(defstatement | assignStatement

	def vartype: Parser[Tree] = "[" ~ word ~ "]" ^^ { case _ ~ valueNode(w, ns) ~ _ => valueNode("array(" + w + ")", ns) })("assign") |
		"(" ~ opt(word) ~ rep("," ~ word) ~ ")" ~ "=>" ~ word ^^ {
			case _ ~ None ~ l ~ _ ~ _ ~ valueNode(ret, _) => valueNode("()=>" + ret, null)
			case _ ~ Some(valueNode(w1, _)) ~ l ~ _ ~ _ ~ valueNode(ret, _) => {
				val lString = l.map { case s ~ valueNode(e, _) => s + e }.mkString
				valueNode("(" + w1 + lString + ")=>" + ret, null)
			}
		} | word

	def defstatement: Parser[Tree] = debug("var" ~ word ~ opt(":" ~ vartype) ~ "=" ~ (objectdef | exp | block | ignoreStatement) ~ ";" ^^ {
		case _ ~ valueNode(id, _) ~ Some(_ ~ valueNode(ty, _)) ~ _ ~ t ~ _ => assignNode(valueNode(id, ty), t, true, 0, ty)
		case _ ~ s1 ~ None ~ _ ~ t ~ _ => assignNode(s1, t, true, 0, null)
	})("defstatement") | word ~ ":=" ~ (objectdef | exp | block | ignoreStatement) ~ ";" ^^ { case s1 ~ s2 ~ t ~ s3 => assignNode(s1, t, true, 0, null) }

	def shortdefstatement: Parser[Tree] = debug(word ~ ":=" ~ (objectdef | exp | ignoreStatement) ~ ";" ^^ { case s1 ~ s2 ~ t ~ s3 => assignNode(s1, t, true, 0, null) })("shortdefstatement")

	def assignStatement: Parser[Tree] = debug((arrayAccess | identifier) ~ "=" ~ (exp | ignoreStatement) ~ ";" ^^ { case id ~ _ ~ b ~ _ => assignNode(id, b, false, 0, null) })("assignStatement")

	def incrementStatement: Parser[Tree] = debug((arrayAccess | identifier) ~ ("+=" | "-=" | "*=" | "/=" | "%=") ~ exp ~ ";" ^^ {
		case id ~ op ~ b ~ _ =>
			val newBody = binopNode(List(id, b), List(opNode(op.charAt(0).toString, null)), 0, null)
			assignNode(id, newBody, false, 0, null)
	})("incrementStatement")


	// ### ARRAYS ###
	def arrays: Parser[Tree] = debug(arrayrange | arraydef

	def arrayElement: Parser[Tree] = strings | funCall | binop | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s })("arrays")

	def arraydef: Parser[Tree] = debug("[" ~ opt(arrayElement) ~ rep("," ~ arrayElement) ~ "]" ^^ {
		case _ ~ firstopt ~ list ~ _ =>
			firstopt match {
				case None => arrayNode(List(), null)
				case Some(e) =>
					val l = list.map { case _ ~ t => t })("arraydef")
					arrayNode(e :: l, null)
			}
	}

	def arrayrangeNumber: Parser[Tree] = debug(integer | identifier | arrayAccess | ("(" ~ binop ~ ")") ^^ { case _ ~ x ~ _ => x })("arrayrangeNumber")

	def arrayrange: Parser[Tree] = debug(arrayrangeNumber ~ ".." ~ arrayrangeNumber ^^ { case a ~ _ ~ b => rangeNode(a, b, arrayType(intType(null))) })("arrayrange")

	def arrayAccess: Parser[Tree] = debug(identifier ~ "[" ~ arrayElement ~ "]" ^^ { case valueNode(name, _) ~ _ ~ b ~ _ => accessNode(name, b, null) })("arrayAccess")


	// ### FUNCTION ###
	def arg: Parser[Tree] = debug(word ~ ":" ~ vartype ^^ { case valueNode(name, _) ~ s ~ ty => argNode(name, ty) })("arg")

	def func: Parser[Tree] = debug("(" ~ opt(arg) ~ rep("," ~ arg) ~ ")" ~ opt(":" ~ vartype) ~ "=>" ~ (exp | block | ignoreStatement) ^^ {
		case _ ~ Some(arg1) ~ l ~ _ ~ Some(_ ~ ty) ~ _ ~ b =>
			val x = l.map { case s ~ t => t })("func")
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

	def funCall: Parser[Tree] = debug(identifier ~ ":" ~ rep(funArgExp) ^^ { case valueNode(name, _) ~ _ ~ listargs => callNode(name, listargs, false, null) })("funCall")

	def callStatement: Parser[Tree] = debug(funCall ~ ";" ^^ { case callNode(id, args, _, ns) ~ _ => callNode(id, args, true, ns) })("callStatement")


	// ### CONTROL FLOW ###
	def guard: Parser[Tree] = debug(binop | arrayAccess | funCall

	def ifstatement: Parser[Tree] = "if" ~ "(" ~ guard ~ ")" ~ (statement | block) ~ opt("else" ~ (statement | block)) ^^ {
		case _ ~ _ ~ b ~ _ ~ e1 ~ Some(_ ~ e2) => ifNode(b, e1, Some(e2), null)
		case _ ~ _ ~ b ~ _ ~ e1 ~ None => ifNode(b, e1, None, null)
	})("guard")

	def whilestatement: Parser[Tree] = debug("while" ~ "(" ~ guard ~ ")" ~ (statement | block) ^^ { case s1 ~ s2 ~ b ~ s3 ~ e => whileNode(b, e, null) })("whilestatement")

	def forstatement: Parser[Tree] = debug("for" ~ "(" ~ word ~ "in" ~ (arrays | guard) ~ ")" ~ (statement | block) ^^ { case _ ~ _ ~ id ~ _ ~ arr ~ _ ~ body => forNode(id, arr, body, null) })("forstatement")


	// ### OBJECTS ###
	def objectdef: Parser[Tree] = debug("{" ~ rep(objectrow ~ ",") ~ "})("objectdef")" ^^ { case _ ~ args ~ _ => objectNode(null, args.map(_._1), null) }

	def objectrow: Parser[Tree] =
		word ~ ":" ~ func ^^ { case valueNode(name, _) ~ _ ~ functionNode(_, args, body, ns) => functionNode(name, args, body, ns) } |
			word ~ ":" ~ vartype ^^ { case valueNode(name, _) ~ _ ~ valueNode(ty, _) => objectElementNode(name, ty) } |
			"override" ~ op ~ ":" ~ func ^^ { case _ ~ op ~ _ ~ f => overrideNode(op, f, null) }

	def objectinstansarg: Parser[Tree] = debug(binop | objectinstans | arrays | funCall | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s })("objectinstansarg")

	def objectinstans: Parser[Tree] = debug(identifier ~ "(" ~ opt(objectinstansarg) ~ rep("," ~ objectinstansarg) ~ ")" ^^ {
		case valueNode(name, _) ~ _ ~ None ~ _ ~ _ => objectInstansNode(name, List(), null)
		case valueNode(name, _) ~ _ ~ Some(exp1) ~ list ~ _ => objectInstansNode(name, List(exp1) ++ list.map(_._2), null)
	})("objectinstans")

	// ### GENERAL ###
	//exp anything that returns something
	def identifier: Parser[Tree] =
		word ~ rep("." ~ word) ^^ {
			case valueNode(first, _) ~ list => valueNode(first + list.map { case s ~ valueNode(t, _) => s + t }.mkString, null)
		}

	def exp: Parser[Tree] = debug(strings | arrays | objectinstans | func | funCall | binop | arrayAccess | "(" ~ binop ~ ")" ^^ { case _ ~ s ~ _ => s })("exp")

	def block: Parser[Tree] = debug("{" ~ rep(statement) ~ "})("block")" ^^ { case _ ~ s ~ _ => blockNode(s, null) }

	def retStatement: Parser[Tree] = debug("return" ~ exp ~ ";" ^^ { case _ ~ e ~ _ => returnNode(e, null) })("retStatement")

	def statement: Parser[Tree] = debug(ifstatement | forstatement | whilestatement | incrementStatement | assign | retStatement | callStatement | ignoreStatement | exp ^^ { case s => s })("statement")

	// ### MISC ###
	def ignoreStatement: Parser[Tree] = debug("\\?\\$[^\\?]*\\?\\$".r ^^ { case s => lineNode(s.substring(2, s.length - 2), null) })("ignoreStatement")

	//def inlineComment: Parser[Tree] = debug("#^[;]*".r ^^ { case s => lineNode("//"+s.substring(1, s.length - 1), "") })("inlineComment")

	// ### PARSING START HERE ###
	def start: Parser[Tree] = debug(rep(statement) ^^ { case s => blockNode(s, "") })("start")*/
}
