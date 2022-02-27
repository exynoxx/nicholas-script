import scala.util.parsing.combinator.RegexParsers


object Util {
	var ranCounter = 0
	def genRandomName(): String = {
		val ret = "ran" + ranCounter
		ranCounter += 1
		ret
	}
}
