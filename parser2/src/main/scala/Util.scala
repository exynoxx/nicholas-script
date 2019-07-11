import scala.collection.immutable.HashMap

object Util {
    var ranCounter = 0
    def genRandomName ():String = {
        val ret = "ran"+ranCounter
        ranCounter += 1
        ret
    }

    def convertType(s: String): String = s match {
        case "string" => "char *"
        case z => z
    }
}
