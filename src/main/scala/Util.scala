import java.io.{File, PrintWriter}
import scala.collection.immutable.HashSet
import scala.collection.mutable
import scala.io.Source
import scala.util.parsing.combinator.RegexParsers


object Util {

	implicit class TupleAddition(a: (HashSet[String], mutable.LinkedHashSet[String])) {
		def ++(b: (HashSet[String], mutable.LinkedHashSet[String])) = (a._1 ++ b._1, a._2 ++ b._2)
	}

	var ranCounter = 0
	def genRandomName(): String = {
		val ret = "_NSran" + ranCounter
		ranCounter += 1
		ret
	}

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val alltext = bufferedSource.getLines().mkString("\n")
		bufferedSource.close
		alltext
	}

	def writeFile(filename: String, content: String) = {
		val file = new File(filename)
		if (!file.exists()) {
			file.createNewFile()
		}
		val writer = new PrintWriter(file)
		writer.write(content)
		writer.close()
	}

	def extractNode(node: Tree): (Tree, Tree) = node match {
		case sequenceNode(list) =>
			(sequenceNode(list.init), list.last)
		case x =>
			val id = genRandomName()
			val assign = assignNode(wordNode(id), x)
			(assign, wordNode(id))
	}

	def extractTypedNode(node: Tree): (Tree, Tree) = node match {
		case sequenceNode(list) =>
			(sequenceNode(list.init), list.last)
		case typedNode(x,ty) =>
			val id = genRandomName()
			val assign = typedNode(assignNode(wordNode(id), x), ty)
			(assign, wordNode(id))
	}
}
