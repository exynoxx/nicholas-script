import Util.{NSType, arrayTypeNode, functionTypeNode, simpleType, tyToString}

import scala.collection.mutable
import scala.collection.mutable.ListBuffer

class CodeGenRust {

	var objectVariables = mutable.HashMap[String, List[String]]()


	def convertType(t: String): String = {
		t match {
			case "actualstring" => "String"
			case "string" => "String"
			case "int" => "i32"
			case Util.arrayTypePattern(ty) => "Vec<" + convertType(ty) + ">"
			case Util.functionTypePattern(args, ret) => convertFunctionType(t)
			case Util.objectTypePattern(id) => id
			case Util.objectInstansTypePattern(id) => id
			case x => x
		}
	}

	def recursiveFunctionType(t: NSType): String = {
		t match {
			case simpleType(s, _) => convertType(s)
			case arrayTypeNode(ty) => "Vec<" + recursiveFunctionType(ty) + ">"
			case functionTypeNode(args, ty) => "fn(" + args.map(recursiveFunctionType).mkString(",") + ")->" + recursiveFunctionType(ty)
		}
	}

	def convertFunctionType(t: String): String = {
		recursiveFunctionType(Util.getType(t))
	}

	def convertArgType(t: String): String = {
		t match {
			case "string" => "&mut String"
			case Util.arrayTypePattern(ty) => "&mut Vec<" + convertType(ty) + ">"
			case x => convertType(x)
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
			case valueNode(value, ns) =>
				ns match {
					case "actualstring" => value + ".to_string()"
					case _ => value
				}
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

				val mutOp = if (ty == "string") "mut " else "&mut "
				val s1 = "for " + mutOp + v + " in " + recurse(a) + ".iter_mut() {\n"
				val s2 = recurse(b)
				val s3 = "}\n"
				s1 + s2 + s3

			case argNode(name, ns) => name + ":" + convertArgType(ns)
			case specialArgNode(content, ns) => content

			case functionNode(id, args, body, ns) =>
				val retTy = recursiveFunctionType(Util.getType(ns).ty) match {
					case "void" => ""
					case null => ""
					case x => "-> " + x
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
					x.nstype match {
						case "string" => "&mut " + xString
						case "actualstring" => "&mut " + xString
						case Util.arrayTypePattern(ty) => "&mut " + xString
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

			case rangeNode(valueNode(a, _), valueNode(b, _), ns) => "(" + a + ".." + b + ").collect::<Vec<i32>>()"

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
				s1+s2+s3

			case x => "//" + x.toString + "\n"
		}
	}

	def gen(AST: Tree): String = {
		val s0 = "#![allow(unused_parens)]\n#![allow(unused_mut)]\n#![allow(non_snake_case)]\n"
		val s1 = "fn main(){\n"
		val s2 = recurse(AST)
		val s3 = "}\n"
		s0 + s1 + s2 + s3
	}

}
