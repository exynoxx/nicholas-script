import java.ANTLRHandler
import java.nio.file.{Files, Paths}
import java.util.regex.Pattern

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

    def removeCCodeBlock(s: String): String = s.replaceAll(":CBLOCKBEGIN:(?:[^\\n]*(\\n+))+:CBLOCKEND:", "")

    /*
    def extractImports(s: String): String = {

        var ret = ""
        val regex = "import\\s+\"(.+)\";".r

        for (m <- regex.findAllMatchIn(s)) {
            val url = m.group(1)
            val fileContent = readFile(url)
            ret += m.matcher.y
        }
        return
    }
    */

    def main(args: Array[String]): Unit = {
        val glue = new ANTLRHandler
        val rootnode = glue.buildNodeTree("6")
        val converter = new JavaToScalaBind
        val codegen = new Codegen
        val semanticChecker = new Semant

        var tree = converter.convert(rootnode)
        tree = semanticChecker.typeAnnotate(tree, Map.empty[String, String])

        (1 to 10).map{ (i) => println(codegen.generateRandomName)}

    }
}