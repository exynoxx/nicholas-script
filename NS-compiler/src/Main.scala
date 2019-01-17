object Main {

    def main(args: Array[String]): Unit = {
        val glue = new ANTLRHandler
        val rootnode = glue.getNodeStructure("6")
        val converter = new JavaToScalaBind
        val semanticChecker = new Semant


        var tree = converter.convert(rootnode)
        tree = semanticChecker.typeAnnotate(tree)
    }
}