import scala.io.Source
import scala.util.parsing.combinator._

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