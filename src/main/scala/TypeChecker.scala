import scala.collection.immutable.{HashMap, HashSet}
import scala.util.control.Exception

class TypeChecker {

	def typecheck(AST: Tree): Tree = {
		val (t, _, _) = typerecurse(AST, AST, HashMap("+" -> intType()))
		t
	}

	//returns (used variables, unused variables)
	def findUnusedVariables(AST: Tree, symbol: HashSet[String]): (HashSet[String], HashSet[String]) = AST match {
		case wordNode(x) =>
			if (symbol.contains(x)) {
				(HashSet(), HashSet())
			} else {
				(HashSet(), HashSet(x))
			}
		case unopNode(op, exp) =>
			findUnusedVariables(exp, symbol)
		case binopNode(op, l, r) =>
			val (lused, lunused) = findUnusedVariables(l, symbol)
			val (rused, runused) = findUnusedVariables(r, symbol)
			(lused ++ rused, lunused ++ runused)
		case assignNode(wordNode(id), body) =>
			val (used, unsued) = findUnusedVariables(body, symbol)
			(used ++ HashSet[String](id), unsued)
		//TODO: assign in one cell used in next
		case arrayNode(elem) =>
			elem.map(findUnusedVariables(_, symbol)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
		case accessNode(arr, index) =>
			val (u, unu) = findUnusedVariables(arr, symbol)
			val (u1, unu1) = findUnusedVariables(index, symbol)
			(u ++ u1, unu ++ unu1)
		//case callNode(blockNode(elems), args) => ()
		case callNode(id, args) =>
			val (u, unu) = args.map(findUnusedVariables(_, symbol)).reduce((a, b) => (a._1 ++ b._1, a._2 ++ b._2))
			val (u_id, unu_id) = findUnusedVariables(id, symbol)
			(u ++ u_id, unu ++ unu_id)
		case blockNode(elems) =>
			var (used, unused) = (symbol, HashSet[String]())
			for (x <- elems) {
				val (u, unu) = findUnusedVariables(x, used)
				used ++= u
				unused ++= unu
			}
			(used, unused)
		case _ => (HashSet[String](), HashSet[String]())
	}

	def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, Type]): (Tree, Type, HashMap[String, Type]) = {
		AST match {
			case wordNode(x) =>
				try {
					(wordNode(x), symbol(x), symbol)
				} catch {
					case _ => (wordNode(x), intType(), symbol)
				}

			case integerNode(x) => (integerNode(x), intType(), symbol)

			case boolNode(x) => (boolNode(x), boolType(), symbol)

			case stringNode(x) => (stringNode(x), stringType(), symbol)

			case unopNode(op, exp) =>

				val (body, typ, nsymbol) = typerecurse(exp, AST, symbol)
				op match {
					case "!" =>
						val fname = typ match {
							case intType() => "_NSfac"
							case boolType() => "_NSboolinv"
						}
						(libraryCallNode(fname, List(body)), typ, nsymbol)
				}

			case binopNode(op, l, r) =>
				val (left, ltyp, _) = typerecurse(l, AST, symbol)
				val (right, rtyp, _) = typerecurse(r, AST, symbol)
				val variable = wordNode(Util.genRandomName())
				val (ret: Tree, typ: Type) = (op, ltyp, rtyp) match {
					case ("*", functionType(), intType()) => (loopNode(variable, integerNode(0), right, integerNode(1), left), voidType)
					case ("*", functionType(), boolType()) => (loopNode(variable, integerNode(0), right, integerNode(1), left), voidType)
					case ("*", intType(), functionType()) => (loopNode(variable, integerNode(0), left, integerNode(1), right), voidType)
					case ("*", boolType(), functionType()) => (loopNode(variable, integerNode(0), left, integerNode(1), right), voidType)
					case ("*", arrayType(), intType()) => (arrayMultNode(left.asInstanceOf[arrayNode].elements.head, right), arrayType())
					case ("*", intType(), arrayType()) => (arrayMultNode(right.asInstanceOf[arrayNode].elements.head, left), arrayType())

					case ("/", functionType(), arrayType()) => (libraryCallNode("_NSmap", List(left,right)), arrayType())

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
				val (_, unusedVariables) = findUnusedVariables(blockNode(children), HashSet())
				val recursedChildren = children.map(x => typerecurse(x, AST, symbol)._1)
				val nChildren = recursedChildren.flatMap{
					case sequenceNode(l)=> l
					case x => List(x)
				}
				(functionNode(unusedVariables.toList.map(wordNode), blockNode(nChildren)), functionType(), symbol)

			case callNode(f, args) =>
				//TODO symbol register each arg if contain assign
				val nargs = args.map(x => typerecurse(x, AST, symbol)._1)
				val (func, ftyp, sym) = typerecurse(f, AST, symbol)

				ftyp match {
					case functionType() =>
						val id = wordNode(Util.genRandomName())
						val assign = assignNode(id,func)
						val call = callNode(id,nargs)
						(sequenceNode(List(assign,call)),ftyp,sym)
					case _ =>
						(callNode(func, nargs), ftyp, sym)
				}


			case arrayNode(elements) =>
				var sym = symbol
				var typ: Type = null
				val nelements = elements.map(x => {
					val (elem, t, s) = typerecurse(x, AST, sym)
					sym = s
					typ = t
					elem
				})
				(arrayNode(nelements), arrayType(), sym)

			case accessNode(arrayId, idx) =>
				//TODO: recurse array
				val typ = arrayId match {
					case arrayNode(elements) => typerecurse(arrayNode(elements), AST, symbol)._2.asInstanceOf[arrayType]
					case wordNode(id) => symbol(id).asInstanceOf[arrayType]
				}
				//TODO: recurse idx

				(accessNode(arrayId, idx), intType(), symbol)

			case x => (x, voidType(), symbol)
		}
	}
}
