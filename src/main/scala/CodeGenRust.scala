
import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class CodeGenRust {

	var objectVariables = mutable.HashMap[String, List[String]]()

	def convertType(t: Type): String = {
		t match {
			case stringType(_) => "String"
			case explicitStringType(_) => "String"
			case intType(_) => "i32"
			case boolType(_) => "bool"
			case arrayType(ty) => "Vec<" + convertType(ty) + ">"
			case functionType(args, ret) => "fn(" + args.map(convertType).mkString(",") + ")->" + convertType(ret)
			case objectType(id, _, _) => id
			case objectInstansType(id, _, _) => id
			case x => x.toString
		}
	}

	def convertArgType(name:String, ty: Type): String = {
		ty match {
			case stringType(_) => name + ": &mut String"
			case arrayType(ty) => name + ": &mut Vec<" + convertType(ty) + ">"
			case x => "mut " + name + ":" + convertType(x)
		}
	}

	def genPreString(): String = {
		val print = "print := (x:string) => ?$ print!(\"{} \",x); ?$;\n"
		val println = "println := (x:string) => ?$ println!(\"{}\",x); ?$;\n"
		val toString = "toString := (x:int):string => ?$ return x.to_string(); ?$;\n"
		val toInt = "toInt := (x:string):int => ?$ return x.parse::<i32>().unwrap(); ?$;\n"
		print + println + toString + toInt
	}

	def recurse(tree: Tree): String = {
		tree match {
			case assignNode(id, body, deff, _, ns) =>
				val idString = recurse(id)

				val vectorExtension = body.isInstanceOf[arrayNode] match {
					case true => ".to_vec()"
					case false => ""
				}

				val end = recurse(body) + vectorExtension + ";\n"
				val ss = deff match {
					case true => "let mut " + idString + ":" + convertType(ns) + " = " + end
					case false => idString + " = " + end
				}
				ss
			case opNode(body, ns) => body
			case binopNode(numbers, ops, _, ns) =>
				val opsString = ops.map(x => recurse(x)) ++ List("")
				val numbersString = numbers.map(x => recurse(x))
				val flatList = ListBuffer.from(numbersString.zip(opsString).flatMap(tup => List(tup._1, tup._2)))

				var i = 1
				while (i < flatList.size - 1) {
					if (flatList(i) == "**") {

						//TODO: improve

						val postfixType = if (flatList(i - 1).matches("[0-9]+")) {
							"_i32"
						} else {
							""
						}

						val tmp = flatList(i - 1) + postfixType + ".pow(" + flatList(i + 1) + " as u32)"
						flatList.remove(i - 1)
						flatList.remove(i - 1)
						flatList.remove(i - 1)
						flatList.insert(i - 1, tmp)
					}
					i += 2
				}
				"(" + flatList.mkString + ")"
			case valueNode(value, explicitStringType(_)) => value + ".to_string()"
			case valueNode(value, ns) => value
			case ifNode(c, b, elsebody, ns) =>
				val s1 = "if " + recurse(c) + " {\n"
				val s2 = recurse(b)
				val s3 = "}\n"

				val s4 = elsebody match {
					case Some(ss) => "else {\n" + recurse(ss) + "}\n"
					case None => ""
				}
				s1 + s2 + s3 + s4
			case whileNode(c, b, ns) =>
				val s1 = "while " + recurse(c) + " {\n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3

			case forNode(valueNode(v, ty), a, b, ns) =>

				val mutOp = ty match {
					case stringType(null) => "mut "
					case explicitStringType(null) => "mut "
					case _ => "&mut "
				}

				val s1 = "for " + mutOp + v + " in " + recurse(a) + ".iter_mut() {\n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3

			case argNode(name, ns) => convertArgType(name,ns)
			case specialArgNode(content, ns) => content

			case functionNode(id, args, body, ns) =>
				val retTy = ns.ty match {
					case voidType(_) | null => ""
					case x => "-> " + convertType(x)
				}

				val s0 = "fn " + id
				val s1 = "(" + args.map(x => recurse(x)).mkString(",") + ")" + retTy + " {\n"
				val s2 = recurse(body)
				val s3 = "}\n"
				s0 + s1 + s2 + s3

			case blockNode(children, ns) => children.map(x => recurse(x)).mkString

			case callNode(id, args, deff, ns) =>
				val stringArgs = args.map { x =>
					val xString = recurse(x)
					x.ty match {
						case stringType(_) => "&mut " + xString
						case explicitStringType(_) => "&mut " + xString
						case arrayType(_) => "&mut " + xString
						case functionType(_, stringType(_)) => "&mut " + xString
						case functionType(_, arrayType(_)) => "&mut " + xString
						case _ => xString
					}
				}

				val ret = id + "(" + stringArgs.mkString(",") + ")"
				deff match {
					case true => ret + ";\n"
					case false => ret
				}

			case returnNode(body, ns) => recurse(body) + "\n"

			case lineNode(text, ns) => text + "\n"

			case arrayNode(elements, ns) =>
				val stringElements = elements.map {
					case accessNode(id, index, ns) => recurse(accessNode(id, index, ns)) + ".clone()"
					case x => recurse(x)
				}
				"vec![" + stringElements.mkString(",") + "]"

			case rangeNode(valueNode(a, _), valueNode(b, _), ns) => "(" + a + ".." + b + ").collect::<Vec<" + convertType(intType(null)) + ">>()"

			case accessNode(name, idx, _) =>
				val idxString = recurse(idx)
				name + "[" + idxString + " as usize]"

			case objectNode(id, rows, ns) =>
				val s1 = "struct " + id + " {\n"
				val s2 = rows.map({
					case objectElementNode(name, ns) =>
						objectVariables.get(id) match {
							case Some(nameList) => objectVariables.update(id, nameList ++ List(name))
							case None => objectVariables += (id -> List(name))
						}
						name + ":" + convertType(ns) + ","
				}).mkString("\n")
				val s3 = "}\n"
				s1 + s2 + s3
			//case objectElementNode(name, ns) => name + ":" + convertType(ns) + ","

			case objectInstansNode(name, args, ns) =>
				name + "{" + objectVariables(name).zip(args.map(recurse)).map { case (a, b) => a + ":" + b }.mkString(",") + "}"

			case objectAssociatedFunctionNode(name, func, ns) =>
				val s1 = "impl " + name + "{\n"
				val s2 = func.map(recurse).mkString("\n")
				val s3 = "}\n"
				s1 + s2 + s3

			case overrideNode(op, f, ns) =>
				val rustString = op match {
					case opNode("+", _) => "Add"
					case opNode("-", _) => "Sub"
					case opNode("*", _) => "Mul"
					case opNode("/", _) => "Div"
				}


				val objectID = ns match {
					case objectType(id,_,_) => id
				}

				val (args, body, retTy) = f match {
					case functionNode(_, a, b, ty) =>
						val c = ty.ty match {
							case voidType(_) | null => ""
							case objectType(id,_,_) => id
						}
						(a, b, c)
				}
				val other = args(0) match {
					case argNode(n, _) => n
				}

				val s0 = "impl std::ops::" + rustString + " for " + objectID + "{\n"
				val s1 = "type Output = " + retTy + ";\n"
				val s2 = "fn " + rustString.toLowerCase + "(self, " + other + ":Self) ->" + retTy + "{\n"
				val s3 = recurse(body)
				val s4 = "}\n}\n"
				s0 + s1 + s2 + s3 + s4


			case x => "//" + x.toString + "\n"
		}
	}

	def gen(AST: Tree): String = {
		val s0 = "#![allow(unused_parens)]\n#![allow(unused_mut)]\n#![allow(non_snake_case)]\n#![allow(non_camel_case_types)]\n#![allow(dead_code)]\n"
		val s1 = "fn main(){\n"
		val s2 = recurse(AST)
		val s3 = "}\n"
		s0 + s1 + s2 + s3
	}

}
