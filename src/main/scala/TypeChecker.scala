import java.util.NoSuchElementException

import scala.collection.immutable.HashMap
import scala.collection.{immutable, mutable}

class TypeChecker {


	def expandObjectVariables(objectID: String, varName: String, symbol: HashMap[String, Type]): HashMap[String, Type] = {
		val localSymbol = mutable.HashMap[String, Type]()
		val pattern = (objectID + "::(\\w+)").r
		symbol.keys.foreach {
			case k@pattern(variable) =>
				localSymbol += (varName + "." + variable -> symbol(k))
			case _ => ()
		}
		localSymbol.to(HashMap)
	}

	def typecheck(AST: Tree): Tree = {
		val (t, _) = typerecurse(AST, AST, HashMap())
		t
	}

	//used to infer type of function with recursive return type. used in case functionNode
	/*def findTypeRecursive(AST: Tree, symbol: HashMap[String, Type]): (Boolean, Type) = {
		AST match {
			case returnNode(b, ty) =>
				b match {
					case functionNode(_, _, _, _) => return (false, null)
					case _ => ()
				}
				val (typedbody, _) = typerecurse(b, null, symbol)
				(true, typedbody.ty)

			case ifNode(_, b, eb, _) =>
				findTypeRecursive(b, symbol) match {
					case (true, ty) => (true, ty)
					case (false, _) => eb match {
						case Some(b) => findTypeRecursive(b, symbol)
						case _ => (false, null)
					}
				}
			case whileNode(_, b, _) => findTypeRecursive(b, symbol)
			case forNode(_, _, b, _) => findTypeRecursive(b, symbol)
			case blockNode(children, _) => {
				children.foreach(e => {
					val (t, ty) = findTypeRecursive(e, symbol)
					if (t) return (true, ty)
				})
				(false, null)
			}
			case _ => (false, null)
		}
	}*/

	def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, Type]): (Tree, HashMap[String, Type]) = {
		AST match {
			case valueNode(value, ns) =>

				val newns = if (value == "true" || value == "false") {
					boolType(null)
				} else {
					ns match {
						case null =>
							symbol.get(value) match {
								case Some(v) => v
								case None => throw new NoSuchElementException(value + " not found in symbol l25")
							}
						case _ => ns
					}
				}
				(valueNode(value, newns), symbol)

			case binopNode(numbers, ops, idx, ns) =>
				val newNum: List[Tree] = numbers.map(e => typerecurse(e, AST, symbol)._1)
				val stringTypeExist = newNum.map(e => e.ty).exists {
					case stringType(null) => true
					case _ => false
				}
				val boolTypeExist = ops.map(e => e.ty).exists {
					case boolType(null) => true
					case _ => false
				}
				val ty =
					if (stringTypeExist) {
						stringType(null)
					} else if (boolTypeExist)
						boolType(null)
					else {
						newNum.head.ty match {
							case explicitStringType(_) => stringType(null)
							case x => x
						}
					}

				(binopNode(newNum, ops, idx, ty), symbol)

			case assignNode(id, body, deff, idx, ns) =>
				val (btree, updatedSymbol) = typerecurse(body, AST, symbol)

				val ty = ns match {
					case null => btree.ty match {
						case explicitStringType(_) => stringType(null)
						case x => x
					}
					case x => x
				}

				val newid = id match {
					case valueNode(n, null) => valueNode(n, ty)
					case accessNode(n, localidx, null) => accessNode(n, localidx, ty)
					case x => x
				}

				val textid = id match {
					case valueNode(rid, _) => rid
					case x => x.toString
				}

				val s = ty match {
					case objectInstansType(id, _, _) => expandObjectVariables(id, textid, symbol) ++ updatedSymbol ++ HashMap(textid -> ty)
					case _ => updatedSymbol ++ HashMap(textid -> ty)
				}

				(assignNode(newid, btree, deff, idx, ty), s)
			case functionNode(originalID, args, body, ns) =>
				//get id from assign parent

				val id = originalID match {
					case null => parent match {
						case assignNode(valueNode(name, _), _, _, _, _) => name
						case _ => "ret"
					}
					case something => something
				}

				//update symbol table with args
				var localSymbol = symbol.to(mutable.HashMap)
				var globalSymbol = symbol.to(mutable.HashMap)

				var i = -1
				args.foreach {
					case argNode(name: String, ty) =>
						i += 1
						ty match {
							case objectType(idstring, _, _) =>
								//if type of arg is object, make object variables visible
								localSymbol ++= expandObjectVariables(idstring, name, symbol)
							case _ => ()
						}
						localSymbol += (name -> ty)
						globalSymbol += ((id + "::" + i) -> ty)
				}

				//is ret type defined in syntax? no: find it.
				/*val retTy = ns match {
					case null => findTypeRecursive(body)
					case x => x.ty
				}*/

				//recurse body
				val symbolMapType = ns match {
					case null => functionType(null,intType(null))
					case x => x
				}
				localSymbol += (id -> symbolMapType)
				val (fbody, _) = typerecurse(body, AST, localSymbol.to(HashMap))

				val retTy = ns match {
					case null => fbody.ty
					case x => x.ty
				}

				val ty = functionType(args.map(e => e.ty), retTy)
				(functionNode(id, args, fbody, ty), globalSymbol.to(HashMap))

			//case argNode(name, ns) =>
			case blockNode(children, _) =>
				var s = symbol
				val newkids = children.map { e =>
					val (t, sym) = typerecurse(e, AST, s)
					s = s ++ sym
					t
				}
				var ns: Type = voidType(null)
				newkids.foreach {
					case returnNode(b, ty) =>
						ns = ty match {
							case voidType(_) => ns
							case x => x
						}

					case _ =>
				}
				(blockNode(newkids, ns), s)

			case ifNode(c, b, Some(els), ns) =>
				val (ifbody, _) = typerecurse(b, AST, symbol)
				val (elsbody, _) = typerecurse(els, AST, symbol)
				(ifNode(c, ifbody, Some(elsbody), ifbody.ty), symbol)
			case ifNode(c, b, None, ns) =>
				val (ifbody, _) = typerecurse(b, AST, symbol)
				(ifNode(c, ifbody, None, ifbody.ty), symbol)
			case whileNode(c, b, ns) =>
				val (nc, _) = typerecurse(c, AST, symbol)
				val (nb, _) = typerecurse(b, AST, symbol)
				(whileNode(nc, nb, ns), symbol)
			case forNode(valueNode(variable, _), arr, body, ns) =>
				//val (nvar, _) = typerecurse(variable, AST, symbol)
				val (narr, _) = typerecurse(arr, AST, symbol)
				val varTy = narr.ty match {
					case arrayType(t) => t
				}
				val (nbody, _) = typerecurse(body, AST, symbol ++ HashMap(variable -> varTy))

				(forNode(valueNode(variable, varTy), narr, nbody, voidType(null)), symbol)
			case callNode(id, args, deff, ns) =>
				var i = -1
				val newargs = args.map { e =>
					val (t, _) = typerecurse(e, AST, symbol)
					t match {
						case arrayNode(elem, _) =>
							i += 1
							val newTy = symbol.get(id + "::" + i).get
							arrayNode(elem, newTy)
						case x =>
							i += 1
							x
					}
				}
				val ty = symbol.get(id) match {
					case Some(t) => t.ty
					case None => println(id + " not found in symbol")
						throw new NoSuchElementException
				}

				(callNode(id, newargs, deff, ty), symbol)
			case returnNode(body, ns) =>
				val (newbody, _) = typerecurse(body, AST, symbol)
				(returnNode(newbody, newbody.ty), symbol)

			case arrayNode(elem, ns) =>
				val newelem = elem.map(e => typerecurse(e, AST, symbol)._1)
				val newTy = newelem.size match {
					case 0 => ns
					case _ => arrayType(newelem.head.ty)
				}
				(arrayNode(newelem, newTy), symbol)

			case accessNode(name, index, ns) =>
				val arrayName = symbol.get(name).get
				val newTy = arrayName match {
					case arrayType(t) => t
				}
				val (idx, _) = typerecurse(index, AST, symbol)
				(accessNode(name, idx, newTy), symbol)


			case anonNode(args, body, ns) =>
				val tmp: Tree = functionNode(null, args, body, ns)
				val result = typerecurse(tmp, AST, symbol)._1 match {
					case functionNode(_, newargs, newbody, ty) => anonNode(newargs, newbody, ty)
				}
				(result, symbol)

			case objectNode(_, rows, ns) =>
				val id = parent match {
					case assignNode(valueNode(name, _), _, _, _, _) => name
				}

				var globalSymbol = symbol.to(mutable.HashMap)
				var localSymbol = symbol.to(mutable.HashMap)

				//put struct variables and object definition into local scope
				rows.foreach {
					case objectElementNode(name, ty) =>
						localSymbol += ("self." + name -> ty)
						localSymbol += (name -> ty)
						localSymbol += ((id + "::" + name) -> ty)
					case functionNode(name, _, _, ty) =>
						localSymbol += ((id + "::" + name) -> ty)
					case x => ()
				}
				localSymbol += (id -> objectType(id, null, null))
				localSymbol += ("self" -> objectType(id, null, null))

				//typecheck functions with local var's in scope
				val newrows = rows.map {
					case overrideNode(op, f, _) =>
						val (tyF, _) = typerecurse(f, AST, localSymbol.to(HashMap))
						overrideNode(op, tyF, objectType(id, null, null))
					case x => typerecurse(x, AST, localSymbol.to(HashMap))._1
				}

				//put type of vars and funcs in global scope
				newrows.foreach {
					case objectElementNode(name, ty) =>
						globalSymbol += ((id + "::" + name) -> ty)
					case functionNode(name, _, _, ty) =>
						globalSymbol += ((id + "::" + name) -> ty)
					case _ => ()
				}

				val ty = objectType(id, newrows.map(t => t.ty), null)
				(objectNode(id, newrows, ty), globalSymbol.to(HashMap))


			case objectInstansNode(id, args, ns) =>
				//get all variables in object type
				var s = symbol.to(mutable.HashMap)
				val pattern = (id + "::(\\w+)").r
				symbol.keys.foreach {
					case k@pattern(variable) => s += (variable -> symbol(k))
					case _ => ()
				}
				val localSym = s.to(immutable.HashMap)
				val newargs = args.map {
					typerecurse(_, AST, localSym)._1
				}

				val ty = objectInstansType(id, newargs.map(t => t.ty), null)
				(objectInstansNode(id, newargs, ty), symbol)


			case overrideNode(op, f, ns) =>
				//TODO: do this right
				(overrideNode(op, f, ns), symbol)


			case lineNode(t, ns) => (lineNode(t, voidType(null)), symbol)
			case x => (x, symbol)
		}
	}
}
