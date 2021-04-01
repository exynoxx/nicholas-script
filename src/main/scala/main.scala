import java.io.{File, PrintWriter}


import scala.io.Source
import sys.process._

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
		val printer = new TreePrinter
		val parser = new Parser
		val typeChecker = new TypeChecker
		val treeAugmenter = new TreeAugmenter
		val codeGen = new CodeGenRust

		val inputFile = "src/main/scala/examples/func.ns"
		val outputFile = "out/output.rs"

		val in = codeGen.genPreString() + readFile(inputFile)
		val AST: Tree = parser.parse(parser.start, in) match {
			case parser.Success(t, _) =>
				println("success")
				t
			/*case f: parser.NoSuccess => println("error: " + f.msg)
				nullLeaf()
			case parser.Failure(msg1, msg2) => println(s"Error: $msg1, $msg2")
				nullLeaf()
			case parser.Error(msg1, msg2) => println(s"Error: $msg1, $msg2")
				nullLeaf()*/
			case x => println("Error in .parse: " + x)
				nullLeaf()
		}
		printer.print(AST)
		val typedTree = typeChecker.typecheck(AST)
		printer.print(typedTree)
		val augmentedTree = treeAugmenter.augment(typedTree)
		printer.print(augmentedTree)
		val ret = codeGen.gen(augmentedTree)
		writeFile(outputFile, ret)

		val f = ("rustc " + outputFile + " --out-dir out").!
		println(f)


	}
}