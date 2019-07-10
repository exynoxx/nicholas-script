import scala.collection.immutable.HashMap

object Util {
    def convertType(s: String): String = s match {
        case "string" => "char *"
        case z => z
    }
}
