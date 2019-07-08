class TypeChecker {

	def typerecurse (AST:Tree, parent:Tree):Tree = {
		AST match {
			case valueNode(value, ns) => if (ns.matches("\\d+")) valueNode(value,"int") else valueNode(value,"string")
			case binopNode(l, r, o, ns) =>
				if (l.nstype == "int") binopNode(l,r,o,"int")
				else binopNode(l,r,o,"boolean")
			//case opNode(op, _) => codeblock(ret = op)
			case assignNode(id, body, ns) =>
				val btree = typerecurse(body,AST)
				assignNode(id,btree,btree.nstype)
			case functionNode(_, args, body, ns) =>
				val id = parent match {case assignNode(name,_,_) => name}
				val fbody = typerecurse(body,AST)
				functionNode(id,args,fbody,fbody.nstype)
			//case argNode(name, ns) =>
			case blockNode(children, ns) =>
				val newkids = children.map(e => typerecurse(e,AST))
				blockNode(newkids,"")
			case ifNode(c,b,els,ns) =>
				val ifbody = typerecurse(b,AST)
				val elsbody = els match {case Some(els) => Some(typerecurse(els,AST))
										case None => None}
				ifNode(c,b,elsbody,b.nstype)
			case whileNode(c,b,ns) =>
				whileNode(c, typerecurse(b,AST), ns)
		}
	}

}
