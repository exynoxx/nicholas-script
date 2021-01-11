object Util {
    val stringPattern = "(\"(?:[^\"\\\\]|\\\\.)*\")".r
	val arrayTypePattern = "array\\((\\w+)\\)".r
	val functionTypePattern1 = "\\([\\w\\s,]+\\)\\s*=>\\s*(\\w+)".r
	val functionTypePattern2 = "\\(([\\w\\s,]+)\\)\\s*=>\\s*(\\w+)".r
	var ranCounter = 0
    def genRandomName ():String = {
        val ret = "ran"+ranCounter
        ranCounter += 1
        ret
    }
}
