class Semant {

    def typeAnnotate (tree:Tree): Unit = {
        tree match {
            case programNode(children) => {
                val c = children.map(typeAnnotate(_))
                return programNode(c)
            }
            case assignNode(id,body) => {

            }
        }
    }

}
