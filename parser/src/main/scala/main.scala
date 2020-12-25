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
		val p = new Parser
		val t = new TypeChecker
		val cg = new CodeGenRust

		val inputFile = "src/main/scala/basic.ns"
		val outputFile = "out/output.rs"

		//JS
		//val prestring = "print := (x:string) => {\n\t?$ console.log(x) ?$\n};\n"
		//rust
		val prestring = "print := (x:string) => {\n?$ println!(\"{}\",x); ?$\n};\n"


		val in = prestring+readFile(inputFile)
		val AST:Tree = p.parse(p.start,in) match {
			case p.Success(t, _) =>
				println("success")
				t
            case f : p.NoSuccess => println("error: "+f.msg)
                                        nullLeaf()
			case p.Failure(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
			case p.Error(msg1,msg2) => println(s"Error: $msg1, $msg2")
										nullLeaf()
			case x => println("Error: "+x)
										nullLeaf()
		}

        val tree = t.augment(t.typecheck(AST))
		printer.print(tree)
        val ret = cg.gen(tree)
        writeFile(outputFile,ret)

        val f = "rustc out/output.rs --out-dir out".!
        println(f)

	}
}