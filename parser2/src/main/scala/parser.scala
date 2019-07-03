import scala.collection.mutable.ArrayBuffer
import scala.collection.mutable.Map

class parser() {
	var stack = ArrayBuffer[String]()
	var rules = Map[Array[String], String]()
	var input = Array[String]()
	var idx = 0

	def addRule(rule: String) = {
		val (name: String, body: String) = rule.split("::=") match {
			case Array(i, j) => (i, j)
		}
		val allrules = body.split("\\|")
		for (e <- allrules) {
			val k = e.split("\\s+")
			rules += (k -> name)
		}
	}

	def parse(input: Array[String]): Tree = {
		this.input = input
		while(true) {
			if (!reduce()) {
				if (!shift()) {
					println("done!")
					return nullLeaf()
				}
			}
		}
		nullLeaf()
	}

	def shift():Boolean= {
		if (idx < input.length){
			stack += input(idx)
			idx += 1
			println("---------------")
			val str = stack.foldLeft("") { (a: String, b: String) => a + b + " " }
			println(str)
			true
		} else {
			false
		}
	}

	def compareToken(a: String, b: String): Boolean = {
		val boola = a.matches("STRING\\([\\*\\w]+\\)")
		val boolb = b.matches("STRING\\([\\*\\w]+\\)")
		val boolc = a.matches("INT\\([\\d]+\\)")
		val boold = b.matches("INT\\([\\*\\d]+\\)")

		//println(a + " + " + b)

		if (boola && boolb) {
			true
		} else if (boolc && boold) {
			true
		} else if (a == b) {
			true
		} else {
			false
		}
	}

	def compareTokens(a: Array[String], b: Array[String]): Boolean = {
		if (a.length == 0 || b.length == 0) return false

		val (x,y) = if (b.length > a.length) (b,a) else (a,b)
		x.slice(x.length - y.length, x.length)
			.zip(y)
			.map { case (x: String, y: String) => compareToken(x, y) }
			.forall(x => x)
	}

	def reduce(): Boolean = {
		//for each rule
		rules.foreach {
			case (body, name) => {
				if (compareTokens(stack.toArray, body)) {
					stack.remove(stack.length - body.length, body.length)
					stack += name


					println("---------------")
					val str = stack.foldLeft("") { (a: String, b: String) => a + b + " " }
					println(str)
					return true
				}
			}
		}

		false
	}
}
