import java.nio.file.{Files, Paths}
import java.util.regex.Pattern

import ANTLR.ANTLRHandler
import sext._

object Main {

    def readFile(path: String): String = {
        val encoded = Files.readAllBytes(Paths.get(path))
        new String(encoded)
    }

    def writeFile(path: String, source: String): Unit = {
        val file = Paths.get(path)
        Files.write(file, source.getBytes)
    }

    def extractCCode(s: String): String = {
        if (s.indexOf(":CBLOCKBEGIN:") == -1) return ""
        val p = Pattern.compile(":CBLOCKBEGIN:(([^\\n]*(\\n+))+):CBLOCKEND:")
        val m = p.matcher(s)
        var ccode = ""
        while ( {
            m.find
        }) ccode += m.group(1)
        ccode
    }

    def removeCCodeBlock(s: String): String = ":CBLOCKBEGIN:(?:[^\\n]*(\\n+))+:CBLOCKEND:".r.replaceAllIn(s, "")

    def extractImports(s: String): String = {

        val regex = "import\\s+\"(.+)\";".r

        regex.replaceAllIn(s, m => readFile(m.group(1)))
    }

    def main(args: Array[String]): Unit = {
        val glue = new ANTLRHandler
        val converter = new JavaToScalaBind
        val codegen = new Codegen
        val semanticChecker = new Semant

        val filecontent = readFile("src/examples/6.ns")
        val extracted = extractImports(filecontent)
        val clean = removeCCodeBlock(extracted)
        val rootnode = glue.buildNodeTree(clean)
        var tree = converter.convert(rootnode)
        println(tree.treeString)
        //val (semantTree,env) = semanticChecker.typeAnnotate(tree, Map.empty[String, String])
    }
}