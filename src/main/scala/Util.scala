import scala.util.parsing.combinator.RegexParsers


object Util {
	val stringPattern = "(\"(?:[^\"\\\\]|\\\\.)*\")".r
	val arrayTypePattern = "array\\((\\w+)\\)".r
	val functionTypePattern = "\\(([^=]*)\\)\\s*=>\\s*(.+)".r
	val objectTypePattern = "object\\((\\w+).*".r
	val objectInstansTypePattern = "objectInstans\\((\\w+).*".r
	var ranCounter = 0

	def genRandomName(): String = {
		val ret = "ran" + ranCounter
		ranCounter += 1
		ret
	}

	trait NSType {
		val ty: NSType
	}

	case class simpleType(stringTy: String, ty: NSType) extends NSType

	case class arrayTypeNode(ty: NSType) extends NSType

	case class functionTypeNode(args: List[NSType], ty: NSType) extends NSType

	case class objectInstansTypeNode(args: List[NSType], ty: NSType) extends NSType

	class TypeParser extends RegexParsers {
		def word: Parser[NSType] = "\\w+".r ^^ { case s => simpleType(s, null) }

		def array: Parser[NSType] = "array" ~ "(" ~ arg ~ ")" ^^ { case _ ~ _ ~ ty ~ _ => arrayTypeNode(ty) }

		def func: Parser[NSType] = "(" ~ opt(arg) ~ rep("," ~ arg) ~ ")" ~ "=>" ~ arg ^^ {
			case _ ~ Some(frst) ~ largs ~ _ ~ _ ~ ty => {
				functionTypeNode(List(frst) ++ largs.map(x => x._2), ty)
			}
			case _ ~ None ~ largs ~ _ ~ _ ~ ty => functionTypeNode(List(), ty)
		}

		def objectInstans: Parser[NSType] = "objectInstans" ~ "(" ~ word ~ rep("," ~ arg) ~ ")" ^^ {
			case _ ~ _ ~ id ~ l ~ _ => objectInstansTypeNode(l.map(x => x._2),id)
		}

		def arg: Parser[NSType] = objectInstans | array | word | func

	}

	def tyToString(tree: NSType): String = {
		tree match {
			case simpleType(s, _) => s
			case arrayTypeNode(ty) => "array(" + tyToString(ty) + ")"
			case functionTypeNode(args, ty) => "(" + args.map(tyToString).mkString(",") + ")=>" + tyToString(ty)
			case objectInstansTypeNode(_,ty) => tyToString(ty)
		}
	}

	val tyParser = new TypeParser

	def getReturnType(inn: String): String = {
		tyParser.parse(tyParser.func, inn) match {
			case tyParser.Success(t, _) => tyToString(t.ty)
			case _ => println("could not parse type: " + inn)
				throw new NullPointerException
		}
	}

	def getType(inn: String): NSType = {
		tyParser.parse(tyParser.func, inn) match {
			case tyParser.Success(t, _) => t
			case _ => throw new NullPointerException
		}
	}

}
