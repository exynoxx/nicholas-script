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
		val printer = new TreePrinter
		val p = new Parser
		val t = new TypeChecker
		val cg = new CodeGenerator

		val in = readFile("src/main/scala/test.ns")
		val AST:Tree = p.parse(p.start,in) match {
			case p.Success(t, _) => t
			case p.Failure(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
			case p.Error(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
		}
		val tyAST:Tree = t.typerecurse(AST,AST)
		print(cg.gen(AST))

	}
}