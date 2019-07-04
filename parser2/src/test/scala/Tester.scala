import org.scalatest.FunSuite

class Tester extends FunSuite {
	val p = new parser(false)
	test("VAR") {
		assert(p.compareToken("VAR", "VAR") === true)
	}
	test("*") {
		assert(p.compareToken("STRING(*)", "STRING(*)") === true)
		assert(p.compareToken("INT(*)", "INT(*)") === true)

	}
	test("diff") {
		assert(p.compareToken("STRING(*)", "STRING(s)") === true)
		assert(p.compareToken("STRING(sad)", "STRING(*)") === true)

	}
	test("int diff") {
		assert(p.compareToken("INT(*)", "INT(234234)") === true)
		assert(p.compareToken("INT(32213)", "INT(*)") === true)

	}

	//compareTokens
	test("a") {
		val a = Array[String]("VAR", "STRING(*)", "EQ", "MULT")
		val b = Array[String]("VAR", "STRING(*)", "EQ")
		val c = Array[String]("MULT")
		assert(p.compareTokens(a, b) === false)
		assert(p.compareTokens(a, c) === true)
		assert(p.compareTokens(c, c) === true)
	}
	test("b") {
		val a = Array[String]("VAR", "STRING(*)", "EQ")
		val b = Array[String]("VAR", "STRING(hejeh)", "EQ")
		assert(p.compareTokens(a, b) === true)
		assert(p.compareTokens(b, b) === true)
	}

	def reduceableLookahead(stackk: Array[String], rule: Array[String], lookaheadBuffer: String): Boolean = {
			val stack = stackk :+ lookaheadBuffer
			val sl = stack.length
			val rl = rule.length
			if (sl == 0 || rl == 0) return false

			var s = stack
			var r = rule
			if (sl < rl) r = rule.slice(0, sl)
			else if (sl > rl) s = stack.slice(sl - rl, sl)

			val b = s.zip(r)
				.map { case (x: String, y: String) => p.compareToken(x, y) }
				.forall(x => x)
			b
		}

	test("reducable lookahead 1") {
		val st1 = Array[String]("VAR", "STRING(*)", "EQ")
		val l1 = "MULT"
		val rul1 = Array[String]("VAR", "STRING(*)", "EQ", "MULT")
		assert(reduceableLookahead(st1,rul1,l1))
	}
	test("reducable lookahead 2") {
		val st1 = Array[String]("VAR", "STRING(*)")
		val l1 = "EQ"
		val rul1 = Array[String]("value", "OP", "value")
		assert(reduceableLookahead(st1,rul1,l1)===false)
	}
	test("reducable lookahead 3") {
		val st1 = Array[String]("MULT", "INT(4)", "PLUS")
		val l1 = "STRING(a)"
		val rul1 = Array[String]("PLUS")
		assert(!reduceableLookahead(st1,rul1,l1))
	}

}

/*
		assert(p.compareToken("VAR", "VAR") === true, "!")
		assert(p.compareToken("STRING(*)", "STRING(*)") == true)
		assert(p.compareToken("STRING(*)", "STRING(s)") == true)
		assert(p.compareToken("STRING(sad)", "STRING(*)") == true)
		assert(p.compareToken("STRING(weoijwf)", "STRING(s)") == false)
		assert(p.compareToken("INT(*)", "INT(234234)") == true)
		assert(p.compareToken("INT(32213)", "INT(*)") == true)
		assert(p.compareToken("INT(*)", "INT(*)") == true)
 */
