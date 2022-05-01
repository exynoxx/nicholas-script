import scala.util.parsing.combinator.RegexParsers


object Util {
	var ranCounter = 0
	def genRandomName(): String = {
		val ret = "_NSran" + ranCounter
		ranCounter += 1
		ret
	}
}
