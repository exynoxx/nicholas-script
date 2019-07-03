import scala.collection.mutable.ArrayBuffer
import scala.collection.mutable.Map

class parser() {
	var stack = ArrayBuffer[String]()
	var rules = Map[Array[String], String]()
	var input = Array[String]()
	var transitionList = ArrayBuffer[String]()
	var idx = 0
	var lookaheadBuffer = ""
	var objPool = Map[Int, Tree]()


	def addRule(rule: String) = {
		val (name: String, body: String) = rule.split("\\s*::=\\s*") match {
			case Array(i, j) => (i, j)
		}
		val allrules = body.split("\\s*\\|\\s*")
		for (e <- allrules) {
			val k = e.split("\\s+")
			rules += (k -> name)
		}
	}

	def parse(input: Array[String]): Tree = {
		this.input = input
		shift()
		while (true) {
			if (!reduce()) {
				if (!shift()) {
					println("done!")
					return nullLeaf()
				}
			}

		}
		nullLeaf()
	}

	def shift(): Boolean = {
		if (idx < input.length) {
			stack += input(idx)
			if (idx < input.length - 1) lookaheadBuffer = input(idx + 1)
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

		val (x, y) = if (b.length > a.length) (b, a) else (a, b)
		x.slice(x.length - y.length, x.length)
			.zip(y)
			.map { case (x: String, y: String) => compareToken(x, y) }
			.forall(x => x)
	}

	def reduceableLookahead(): Boolean = {
		rules.foreach {
			case (body, name) => {
				if (compareTokens(stack.toArray :+ lookaheadBuffer, body)) {
					return true
				}
			}
		}
		false
	}

	def reduce(): Boolean = {

		if (reduceableLookahead()) {
			shift()
		}
		//for each rule
		rules.foreach { case (body, name) => {
			if (compareTokens(stack.toArray, body)) {

				name match {
					case "value" => {
						val tmp = valueNode(stack(stack.length - 1), "int")
						objPool += (stack.length - 1 -> tmp)
					}
					case "op" => {
						val tmp = opNode(stack(stack.length - 1), "")
						objPool += (stack.length - 1 -> tmp)
					}
					case "def" => {
						val name = stack(stack.length - 3)
						val tmp = assignNode(name.substring(7, name.length - 1), objPool(stack.length - 2), "")
						objPool += (stack.length - 2 -> tmp)
					}
					case "binop" => {
						if (body.length == 1) {
							//TODO change
							val tmp = valueNode(stack(stack.length - 1),"int")
							objPool += (stack.length - 1 -> tmp)
						} else {
							val tmp = binopNode(objPool(stack.length - 3), objPool(stack.length - 1), objPool(stack.length - 2), "int")
							objPool += (stack.length - 2 -> tmp)
						}
					}
					case _ =>
				}

				stack.remove(stack.length - body.length, body.length)
				stack += name
				transitionList += name


				println("---------------")
				val str = stack.foldLeft("") { (a: String, b: String) => a + b + " " } + "|" + this.lookaheadBuffer + "|"
				println(str)
				return true
			}
		}
		}

		false
	}
}
