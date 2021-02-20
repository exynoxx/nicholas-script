import java.util.NoSuchElementException

import scala.collection.immutable.HashMap
import scala.collection.{immutable, mutable}
import scala.collection.mutable.ListBuffer

class TypeChecker {

	def typecheck(AST: Tree): Tree = {
		val (t, _) = typerecurse(AST, AST, HashMap())
		t
	}

	def typerecurse(AST: Tree, parent: Tree, symbol: HashMap[String, String]): (Tree, HashMap[String, String]) = {
		AST match {
			case valueNode(value, ns) =>

				val newns = if (value == "true" || value == "false") {
					"bool"
				} else {
					ns match {
						case null =>
							symbol.get(value) match {
								case Some(v) => v match {
									case "actualstring" => "string"
									case x => x
								}
								case None => throw new NoSuchElementException(value + " not found in symbol l25")


							}
						case _ => ns
					}
				}
				(valueNode(value, newns), symbol)

			case binopNode(numbers, ops, idx, ns) =>
				val newNum: List[Tree] = numbers.map(e => typerecurse(e, AST, symbol)._1)
				val stringTypeExist = newNum.exists { case e: Tree => e.nstype == "string" | e.nstype == "actualstring" }
				val boolTypeExist = ops.exists { case opNode(o, t) => t == "bool" }
				val ty: String =
					if (stringTypeExist) {
						"string"
					} else if (boolTypeExist)
						"bool"
					else {
						"int"
					}

				(binopNode(newNum, ops, idx, ty), symbol)

			case assignNode(id, body, deff, idx, ns) =>
				val (btree, updatedSymbol) = typerecurse(body, AST, symbol)
				val ty = ns match {
					case null => btree.nstype match {
						case "actualstring" => "string"
						case x => x
					}
					case x => x
				}
				val newbtree = ns match {
					case Util.arrayTypePattern(arrTy) => btree match {
						case arrayNode(elem, _) => arrayNode(elem, "array(" + arrTy + ")")
					}
					case x => btree
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

				val s = btree match {
					//child is block? extract all variable ID's and add to symbol: textid.[varname]
					case blockNode(_, _) => {
						var childSymbol = HashMap[String, String]()
						updatedSymbol.keys.foreach(e => {
							if (!symbol.contains(e)) {
								childSymbol += (textid + "." + e -> updatedSymbol(e))
							}
						})
						childSymbol ++ HashMap(textid -> ty)
					}
					case _ => updatedSymbol ++ HashMap(textid -> ty)
				}

				(assignNode(newid, newbtree, deff, idx, ty), s)
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
					case argNode(name: String, ty: String) =>
						i += 1
						val newTy = ty match {
							case Util.functionTypePattern(_, t) => t
							case _ => ty
						}
						localSymbol += (name -> newTy)
						globalSymbol += ((id + "::" + i) -> ty)
				}

				//recurse body
				val (fbody, _) = typerecurse(body, AST, localSymbol.to(HashMap))

				//if type defined by syntax, use that
				val ty = ns match {
					case null => "(" + args.map(e => e.nstype).mkString(",") + ")=>" + fbody.nstype
					case x => "(" + args.map(e => e.nstype).mkString(",") + ")=>" + x
				}


				//ret
				(functionNode(id, args, fbody, ty), globalSymbol.to(HashMap))
			//case argNode(name, ns) =>
			case blockNode(children, _) =>
				var s = symbol
				val newkids = children.map { e =>
					val (t, sym) = typerecurse(e, AST, s)
					s = s ++ sym
					t
				}
				var ns = "void"
				newkids.foreach {
					case returnNode(b, ty) => ns = ty
					case _ =>
				}
				(blockNode(newkids, ns), s)
			case ifNode(c, b, Some(els), ns) =>
				val (ifbody, _) = typerecurse(b, AST, symbol)
				val (elsbody, _) = typerecurse(els, AST, symbol)
				(ifNode(c, ifbody, Some(elsbody), ifbody.nstype), symbol)
			case ifNode(c, b, None, ns) =>
				val (ifbody, _) = typerecurse(b, AST, symbol)
				(ifNode(c, ifbody, None, ifbody.nstype), symbol)
			case whileNode(c, b, ns) =>
				val (nc, _) = typerecurse(c, AST, symbol)
				val (nb, _) = typerecurse(b, AST, symbol)
				(whileNode(nc, nb, ns), symbol)
			case forNode(valueNode(variable, _), arr, body, ns) =>
				//val (nvar, _) = typerecurse(variable, AST, symbol)
				val (narr, _) = typerecurse(arr, AST, symbol)
				val varTy = narr.nstype match {
					case Util.arrayTypePattern(ty) => ty
				}
				val (nbody, _) = typerecurse(body, AST, symbol ++ HashMap(variable -> varTy))

				(forNode(valueNode(variable, varTy), narr, nbody, "void"), symbol)
			case callNode(id, args, deff, ns) =>
				var i = -1
				val newargs = args.map { e =>
					val (t, _) = typerecurse(e, AST, symbol)
					t match {
						case arrayNode(elem, Util.arrayTypePattern(ty)) =>
							i += 1
							val newTy = symbol.get(id + "::" + i).get
							arrayNode(elem, newTy)
						case x =>
							i += 1
							x
					}
				}
				val ty = symbol.get(id) match {
					case Some(t) => Util.getReturnType(t)
					case None => println(id + " not found in symbol")
						throw new NoSuchElementException
				}

				(callNode(id, newargs, deff, ty), symbol)
			case returnNode(body, ns) =>
				val (newbody, _) = typerecurse(body, AST, symbol)
				(returnNode(newbody, newbody.nstype), symbol)

			case arrayNode(elem, ns) =>
				val newelem = elem.map(e => typerecurse(e, AST, symbol)._1)
				var types = new mutable.HashMap[String, Int]()
				newelem.foreach {
					case x => types.updateWith(x.nstype)({
						case Some(count) => Some(count + 1)
						case None => Some(1)
					})
				}
				val newTy = types.size match {
					case 0 => ns
					case _ => types.maxBy(_._2)._1 match {
						case "actualstring" => "array(string)"
						case x => "array(" + x + ")"
					}
				}
				(arrayNode(newelem, newTy), symbol)

			case accessNode(name, index, ns) =>
				val arrayName = symbol.get(name).get
				val newTy = arrayName match {
					case Util.arrayTypePattern(ty) => ty
				}
				val (idx, _) = typerecurse(index, AST, symbol)
				(accessNode(name, idx, newTy), symbol)


			case anonNode(args, body, ns) =>
				var localSymbol = symbol.to(mutable.HashMap)

				var i = -1
				args.foreach {
					case argNode(name: String, ty: String) =>
						i += 1
						localSymbol += (name -> ty)
				}

				//recurse body
				val (fbody, _) = typerecurse(body, AST, localSymbol.to(HashMap))

				//if type defined by syntax, use that
				val ty = ns match {
					case null => fbody.nstype
					case x => x
				}
				(anonNode(args, fbody, ty), symbol)

			case objectNode(_, rows, ns) =>
				val id = parent match {
					case assignNode(valueNode(name, _), _, _, _, _) => name
				}

				var globalSymbol = symbol.to(mutable.HashMap)
				var localSymbol = symbol.to(mutable.HashMap)

				rows.foreach {
					case objectElementNode(name, ty: String) =>
						globalSymbol += ((id + "::" + name) -> ty)
						localSymbol += (name -> ty)
					case x => ()
				}

				val newrows = rows.map {
					case x => typerecurse(x, AST, localSymbol.to(HashMap))._1
				}

				val ty = "object(" + id + "," + rows.map(t => t.nstype) + ")"
				(objectNode(id, newrows, ty), globalSymbol.to(HashMap))
			//case objectElementNode(name, ns) =>
			case objectInstansNode(id, args, ns) =>

				var s = symbol.to(mutable.HashMap)
				val pattern = (id + "::(\\w+)").r
				symbol.keys.foreach {
					k => {
						k match {
							case pattern(variable) => s += (variable -> symbol(k))
							case _ => ()
						}
					}
				}
				val localSym = s.to(immutable.HashMap)

				var i = -1
				val newargs = args.map { e =>
					val (t, _) = typerecurse(e, AST, localSym)
					t match {
						case arrayNode(elem, Util.arrayTypePattern(ty)) =>
							i += 1
							val newTy = symbol.get(id + "::" + i).get
							arrayNode(elem, newTy)
						case x =>
							i += 1
							x
					}
				}
				val ty = "objectInstans(" + id + "," + newargs.map(t => t.nstype) + ")"
				(objectInstansNode(id, newargs, ty), symbol)

			case lineNode(t, ns) => (lineNode(t, "void"), symbol)
			case x => (x, symbol)
		}
	}
}
