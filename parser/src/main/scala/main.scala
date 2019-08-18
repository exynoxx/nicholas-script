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
    def writeFile(filename:String, content:String) = {
        val writer = new PrintWriter(new File(filename))
        writer.write(content)
        writer.close()

    }

	def main(args: Array[String]): Unit = {
		val printer = new TreePrinter
		val p = new Parser
		val t = new TypeChecker
		val cg = new CodeGenJS //new CodeGenC

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

        val tree = t.augment(t.typecheck(AST))
		printer.print(tree)
        val ret = cg.gen(tree)
        writeFile("out/out.js",ret)

        //val f = "gcc out/out.c".!
        //println(f)

	}
}