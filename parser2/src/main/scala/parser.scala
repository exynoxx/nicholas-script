import scala.collection.mutable.ArrayBuffer
import scala.collection.mutable.Map

class parser(debug: Boolean) {
	var stack = ArrayBuffer[String]()
	var rules = Map[Array[String], String]()
	var input = Array[String]()
	//var transitionList = ArrayBuffer[String]()
	var idx = 0
	var lookaheadBuffer = ""
	var objPool = ArrayBuffer[Tree]()


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

	def printRules(): Unit = {
		this.rules.foreach { case (k, v) => println(k.foldLeft("") { (a: String, b: String) => a + b + " " } + " -> " + v) }
	}

	def parse(input: Array[String]): Tree = {
		this.input = input
		shift()
		while (true) {
			if (!reduce(debug)) {
				if (!shift(debug)) {
					println("done!")
					return this.objPool(objPool.length - 1)
				}
			}

		}
		nullLeaf()
	}

	def shift(debug: Boolean = false): Boolean = {
		if (idx < input.length) {
			stack += input(idx)
			objPool += nullLeaf()
			if (idx < input.length - 1) lookaheadBuffer = input(idx + 1)
			idx += 1
			if (debug) {
				println("---------------")
				val str = stack.foldLeft("") { (a: String, b: String) => a + b + " " }
				println(str)
			}
			true
		} else {
			false
		}
	}

	def compareToken(a: String, b: String): Boolean = {
		val boola = a.matches("STRING\\([\\*\\w]+\\)")
		val boolb = b.matches("STRING\\([\\*\\w]+\\)")
		val boolc = a.matches("INT\\([\\*\\d]+\\)")
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

	def compareTokens(stack: Array[String], rule: Array[String]): Boolean = {
		val sl = stack.length
		val rl = rule.length
		if (sl == 0 || rl == 0) return false
		if (sl < rl) return false

		var s = stack
		if (sl > rl) s = stack.slice(sl - rl, sl)
		s.zip(rule)
			.map { case (x: String, y: String) => compareToken(x, y) }
			.forall(x => x)
	}

	/*
val (x, y) = if (b.length > a.length) (b, a) else (a, b)
x.slice(x.length-y.length, x.length)
.zip(y)
.map { case (x: String, y: String) => compareToken(x, y) }
.forall(x => x)
*/


	def reduceableLookahead(): Boolean = {
		//IS STACK SUBSECTION OF LONGER RULE!?
		var stackMatchRules = rules.filter(
			{
				case (rule, name) => {
					val sl = stack.length
					val rl = rule.length
					if (sl == 0 || rl == 0) return false

					if (sl <= rl) {
						val r = rule.slice(0, sl)
						stack.zip(r)
							.map { case (x: String, y: String) => compareToken(x, y) }
							.forall(x => x)
					} else false
				}
			})
		stackMatchRules.foreach {
			case (rule, name) =>
				val l = rule.length > stack.length
				val c = compareToken(rule(stack.length), lookaheadBuffer)
				if (l && c) return true
		}
		false
	}

	def reduce(debug: Boolean = false): Boolean = {

		if (reduceableLookahead()) {
			return false
		}
		//for each rule
		rules.foreach { case (body, name) => {

			if (compareTokens(stack.toArray, body)) {

				var flush = true
				val tmp: Tree = name match {
					case "value" => valueNode(stack(stack.length - 1), "")
					case "op" => opNode(stack(stack.length - 1), "")
					case "binop" => {
						if (body.length == 1) objPool(objPool.length - 1)
						else binopNode(objPool(stack.length - 3), objPool(stack.length - 1), objPool(stack.length - 2), "int")
					}
					case "def" => {
						val name = stack(stack.length - 3)
						assignNode(name.substring(7, name.length - 1), objPool(stack.length - 1), "")
					}
					case "defshort" => {
						val name = stack(stack.length - 3)
						assignNode(name.substring(7, name.length - 1), objPool(stack.length - 1), "")
					}
					case "statement" => {
						statementNode(objPool(stack.length - 2), "")
					}
					case _ => {
						flush = false
						nullLeaf()
					}

				}

				stack.remove(stack.length - body.length, body.length)
				stack += name
				//transitionList += name
				if (flush) {
					objPool.remove(objPool.length - body.length, body.length)
					objPool += tmp
				}

				if (debug) {
					println("---------------")
					val str = stack.foldLeft("") { (a: String, b: String) => a + b + " " } + "|" + this.lookaheadBuffer + "|"
					println(str)
				}
				return true
			}
		}
		}

		false
	}
}
