import collection.mutable.Stack
import org.scalatest._
import flatspec._
import main.readFile
import matchers._

class NSTester extends AnyFlatSpec with should.Matchers {
	val printer = new TreePrinter
	val parser = new Parser
	val typeChecker = new TypeChecker
	val treeAugmenter = new TreeAugmenter
	val codeGen = new CodeGenRust

	def transfile(in: String): String = {
		val AST: Tree = parser.parse(parser.start, in) match {
			case parser.Success(t, _) => t
			case x => nullLeaf()
		}
		codeGen.gen(treeAugmenter.augment(typeChecker.typecheck(AST)))
	}


	it should "assign" in {
		transfile("a:= 1+1;") should be("fn main(){\nlet mut a:i32 = (1+1);\n}\n")
	}
}
