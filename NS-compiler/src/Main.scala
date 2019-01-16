object Main {

    def main(args: Array[String]): Unit = {
        val glue = new ANTLRHandler
        val rootnode = glue.getNodeStructure("6")
        //print(rootnode)
    }
}