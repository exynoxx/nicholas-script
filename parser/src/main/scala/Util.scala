

object Util {
    val stringPattern = "(\"(?:[^\"\\\\]|\\\\.)*\")".r
	val arrayTypePattern = "array\\((\\w+)\\)".r
	var ranCounter = 0
    def genRandomName ():String = {
        val ret = "ran"+ranCounter
        ranCounter += 1
        ret
    }
}
