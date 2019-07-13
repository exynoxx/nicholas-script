import scala.io.Source
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
            case f : p.NoSuccess => print("error: "+f.msg)
                                        nullLeaf()
			case p.Failure(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
			case p.Error(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
		}

        println("raw:")
        printer.print(AST)
        println("typecheck:")
        printer.print(t.typecheck(AST))
        println("augment:")
        printer.print(t.augment(t.typecheck(AST)))

        //printer.print(t.augment(t.typecheck(AST)))
        val tree = t.augment(t.typecheck(AST))
		print(cg.gen(tree))

	}
}