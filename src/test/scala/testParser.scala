import org.scalatest.flatspec.AnyFlatSpec
import org.scalatest.matchers.should
import org.scalatest._
import flatspec._
import matchers._

class testParser extends AnyFlatSpec with should.Matchers {

	"assign" should "be correct" in {
		val parser = new Parser
		parser.parse(parser.expression, "a=1").get should be (assignNode(wordNode("a"),integerNode(1)))
		parser.parse(parser.expression, "b=1+2").get should be (assignNode(wordNode("b"),binopNode("+",integerNode(1),integerNode(2))))
		parser.parse(parser.expression, "P=print").get should be (assignNode(wordNode("P"),wordNode("print")))
		parser.parse(parser.expression, "var=f 1 2 3").get should be (assignNode(wordNode("var"),callNode(wordNode("f"),List(integerNode(1),integerNode(2),integerNode(3)))))
	}

	"call" should "be correct" in {
		val parser = new Parser
		parser.parse(parser.expression, "f").get should be (wordNode("f"))
		parser.parse(parser.expression, "f 1 2 3").get should be (callNode(wordNode("f"),List(integerNode(1),integerNode(2),integerNode(3))))
		parser.parse(parser.expression, "f +/[1,2,3] [1]").get should be (callNode(wordNode("f"),List(binopNode("/",wordNode("+"),arrayNode(List(integerNode(1),integerNode(2),integerNode(3)))),arrayNode(List(integerNode(1))))))
2	}

	"array" should "be correct" in {}

	"binop" should "be correct" in {}

	"block" should "be correct" in {}

}
