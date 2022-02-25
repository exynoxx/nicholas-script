import java.io.{File, PrintWriter}
import scala.io.Source

object main {

	def readFile(filename: String): String = {
		val bufferedSource = Source.fromFile(filename)
		val alltext = bufferedSource.getLines().mkString
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

	def main(args: Array[String]): Unit = {
		val parser = new Parser
		val printer = new TreePrinter

		/*val printer = new TreePrinter
		val typeChecker = new TypeChecker
		val treeAugmenter = new TreeAugmenter
		val codeGen = new CodeGenRust

		val inputFile = "src/main/scala/examples/object.ns"
		val outputFile = "out/output.rs"*/

		//val in = "1+2*3 & (5/2) * (1+2)"
		//val in = "{a=[1,2^3,1+2*3 & 5/2];b=1+1}"
		//val in = "{a={1+1}*5;4*{1+1};c=[abc=1,2,3,4]}"
		val in = "{first=+/[1,2,b=3,+/c];c=1+2;([1])*10}"
		val AST: Tree = parser.parse(parser.expression, in) match {
			case parser.Success(t, _) =>
				println("success")
				t
			case x => println("Error in .parse: " + x)
				nullLeaf()
		}
		printer.print(AST)

		/*val typedTree = typeChecker.typecheck(AST)
		//printer.print(typedTree)
		val augmentedTree = treeAugmenter.augment(typedTree)
		printer.print(augmentedTree)
		val ret = codeGen.gen(augmentedTree)
		writeFile(outputFile, ret)

		val f = ("rustc " + outputFile + " --out-dir out").!
		println(f)*/


	}
}