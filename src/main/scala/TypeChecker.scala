import scala.collection.immutable.HashMap

class TypeChecker {

	def typecheck(AST: Tree): Tree = {
		val (t, _,_) = typerecurse(AST, AST, HashMap())
		t
	}

	def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, Type]): (Tree, Type, HashMap[String, Type]) = {
		AST match {
			case wordNode(x) => (wordNode(x), symbol(x), symbol)

			case integerNode(x) => (integerNode(x), intType(), symbol)

			case boolNode(x) => (boolNode(x), boolType(), symbol)

			case stringNode(x) => (stringNode(x), stringType(), symbol)

			case binopNode(op, l, r) =>
				val (left, ltyp, _) = typerecurse(l, AST, symbol)
				val (right, rtyp, _) = typerecurse(r, AST, symbol)
				val variable = wordNode(Util.genRandomName())
				val (ret:Tree, typ:Type) = (op, ltyp, rtyp) match {
					case ("*", functionType(_, _), intType()) => (loopNode(variable, integerNode(0), right, integerNode(1), left), voidType)
					case ("*", functionType(_, _), boolType()) => (loopNode(variable, integerNode(0), right, integerNode(1), left), voidType)
					case ("*", intType(), functionType(_, _)) => (loopNode(variable, integerNode(0), left, integerNode(1), right), voidType)
					case ("*", boolType(), functionType(_, _)) => (loopNode(variable, integerNode(0), left, integerNode(1), right), voidType)
					case ("*", arrayType(t), intType()) => (arrayMultNode(left.asInstanceOf[arrayNode].elements.head, right), arrayType(t))
					case ("*", intType(), arrayType(t)) => (arrayMultNode(right.asInstanceOf[arrayNode].elements.head, left), arrayType(t))

					//case (_, intType, intType) => binopNode(op, left, right)
					case (_, _, _) => (binopNode(op, left, right), ltyp)


					//case (intType, stringType) => //
					//case (intType, boolType) => //true=1
					//case (boolType, intType) => //true=1
					//case (boolType, functionType(_, _)) => if (left==)
				}
				(ret, typ, symbol)


			case assignNode(wordNode(id), b) =>
				val (body, btyp, sym) = typerecurse(b, AST, symbol)
				val newsym = symbol ++ HashMap(id -> btyp)
				(assignNode(wordNode(id), body), btyp, newsym)

			case blockNode(children) =>
				//TODO register arg variables x;y;z;
				//TODO convert to function type
				val nChildren = children.map(x => typerecurse(x, AST, symbol)._1)
				(blockNode(nChildren), blockType(), symbol)

			case callNode(f, args) =>
				//TODO symbol register each arg if contain assign
				val nargs = args.map(x => typerecurse(x, AST, symbol)._1)
				val (func, ftyp, sym) = typerecurse(f, AST, symbol)
				(callNode(func, nargs), ftyp, sym)

			case arrayNode(elements) =>
				var sym = symbol
				var typ:Type = null
				val nelements = elements.map(x => {
					val (elem, t, s) = typerecurse(x, AST, sym)
					sym = s
					typ = t
					elem
				})
				(arrayNode(nelements), arrayType(typ), sym)

			case accessNode(arrayId, idx) =>
				//TODO: recurse array
				val typ = arrayId match {
					case arrayNode(elements) => typerecurse(arrayNode(elements),AST,symbol)._2.asInstanceOf[arrayType].typ
					case wordNode(id) => symbol(id).asInstanceOf[arrayType].typ
				}
				//TODO: recurse idx

				(accessNode(arrayId, idx), typ, symbol)

			case x => (x, voidType(), symbol)
		}
	}
}
