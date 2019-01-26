import java.util.*;

public class BackendC {

    HashMap<Integer, HashMap<String, String>> typesHM = new HashMap<Integer, HashMap<String, String>>();
    Random random = new Random();

    String cCode;

    public BackendC(String cCode) {
        this.cCode = cCode;
    }

    public String generateRandomName() {

        int leftLimit = 97; // letter 'a'
        int rightLimit = 122; // letter 'z'
        int targetStringLength = 5;

        StringBuilder buffer = new StringBuilder(targetStringLength);
        for (int i = 0; i < targetStringLength; i++) {
            int randomLimitedInt = leftLimit + (int) (random.nextFloat() * (rightLimit - leftLimit + 1));
            buffer.append((char) randomLimitedInt);
        }

        //String generatedString = buffer.toString();
        return buffer.toString();
    }

    public String gen(Node root) {
        root = semanticAdjustment(root,null, false, 0);
        CStringBuilder cb = recursive(root);
        String ret = cCode + "\n" + "//signatures \n" + cb.getSignature() + "\n" +
                "//functions \n" + cb.getFunctionImpl() + "\n" +
                "//main \n" + cb.getCode();
        return ret;
    }

    /*
    public void enter (String name, int level, String type) {
        typesHM.put(name+level,type);
    }

    public String getType (String name, int level) {
        return typesHM.get(name+level);
    }*/

    public void pushType(String name, String type, int level) {

        if (typesHM.get(level) == null) {
            typesHM.put(level, new HashMap<>());
        }

        HashMap<String, String> tmp = typesHM.get(level);
        tmp.put(name, type);
    }

    public String getType(String name, int level) {
        String ret = null;
        while (level >= 0) {
            if (ret != null) {
                break;
            } else {
                if (typesHM.get(level) != null) {
                    ret = typesHM.get(level).get(name);
                    return ret;
                }
            }
            level--;
        }
        return ret;
    }

    public void resetLevel(int level) {
        typesHM.get(level).clear();
    }


    public CStringBuilder recursive(Node root) {
        String ending = (root.shouldComma) ? ";\n" : "";
        switch (root.type) {
            case PROGRAM:
                CStringBuilder ret = new CStringBuilder("", "", "", "", "");
                String retCode = "";
                String postCode = "";
                for (Node c : root.children) {
                    CStringBuilder cb = recursive(c);
                    ret = merge(ret, cb);
                    retCode += cb.getPre() + cb.getCode();
                    postCode += cb.getPost();
                }

                for (String s : root.frees) {
                    postCode += s;
                }

                String code = "void main () {\n";
                code += retCode + postCode;
                code += "}\n";
                return new CStringBuilder("", code, "", ret.getSignature(), ret.getFunctionImpl());


            case ASSIGN:
                String name = root.ID;
                String type = root.nstype;

                CStringBuilder body = recursive(root.body);
                if (type.equals("string")) type = "char *";


                String line = "";
                if (root.reassignment) {
                    type = "";

                    if (root.nstype.equals("string")) {
                        line += "free(" + name + ");\n";
                    }

                } else {
                    type += " ";
                }

                line += type + name + " = " + body.getCode() + ending;

                if (root.fundecl) {
                    line = "";
                }
                return new CStringBuilder(body.getPre(), line, body.getPost(), body.getSignature(), body.getFunctionImpl());


            case BINOP:
                if (root.body == null) {
                    //leaf
                    return recursive(root.value);
                } else {
                    if (root.nstype != null && root.nstype.equals("string")) {
                        String sizeline = "";
                        Node nodeBody = root;
                        ArrayList<String> list = new ArrayList<>();
                        while (true) {

                            sizeline += "strlen (" + nodeBody.value.text + ")+";
                            list.add(nodeBody.value.text);

                            if (nodeBody.body != null) {
                                nodeBody = nodeBody.body;
                            } else {
                                sizeline += "0";
                                break;
                            }
                        }

                        String rname1 = generateRandomName();
                        String rname2 = generateRandomName();
                        String finalsizeline = "int " + rname1 + " = " + sizeline + ";\n";
                        String salloc = "char *" + rname2 + " = malloc (" + rname1 + ");\n";
                        String scopy = "";
                        for (String s : list) {
                            scopy += "strcat(" + rname2 + ", " + s + ");\n";
                        }
                        String finalline = rname2;
                        String freeline = "free (" + rname2 + ");\n";
                        return new CStringBuilder(finalsizeline + salloc + scopy, finalline, freeline, "", "");

                    } else {
                        CStringBuilder cbbody = recursive(root.body);
                        CStringBuilder cbsign = recursive(root.sign);
                        CStringBuilder cbvalue = recursive(root.value);

                        CStringBuilder cbret1 = merge(cbvalue, cbsign);
                        CStringBuilder cbret2 = merge(cbret1, cbbody);
                        return cbret2;
                    }


                }
            case SIGN:
                return new CStringBuilder(root.text);

            case VALUE:

                if (root.nstype != null && root.nstype.equals("string") && root.text.contains("\"")) {
                    String rname = generateRandomName();
                    int ssize = root.text.length() - 2;
                    String salloc = "char *" + rname + " = (char *) malloc (" + ssize + ");\n";
                    String scopy = "strcpy(" + rname + ", " + root.text + ");\n";
                    String pre = salloc + scopy;
                    String scode = rname;
                    String post = "free (" + rname + ");\n";
                    return new CStringBuilder(pre, scode, post, "", "");
                }

                return new CStringBuilder(root.text);

            case BLOCK:
                CStringBuilder block = new CStringBuilder("", "", "", "", "");
                String blockCode = "";
                String postBlockCode = "";
                for (Node c : root.children) {
                    CStringBuilder cb = recursive(c);
                    block = merge(block, cb);
                    blockCode += cb.getPre() + cb.getCode();
                    //postBlockCode += cb.getPost();
                }

                String codeblock = "{\n";
                codeblock += blockCode + postBlockCode;
                codeblock += "}\n";
                return new CStringBuilder("", codeblock, "", block.getSignature(), block.getFunctionImpl());

            case FUNCTION:

                String args = "(";
                for (Node c : root.args) {
                    CStringBuilder cb = recursive(c);
                    args += cb.getCode() + ",";
                }
                if (root.args.size() > 0) args = args.substring(0, args.length() - 1);
                args += ")";

                CStringBuilder funcbody = recursive(root.body);
                String cbody = funcbody.getPreCodePost();
                String ftype = root.nstype;
                if (ftype.equals("string")) ftype = "char *";
                String signature = ftype + " " + root.ID + args + ";\n";
                String functionCode = ftype + " " + root.ID + args + cbody;
                return new CStringBuilder(
                        "",
                        "",
                        "",
                        signature + funcbody.getSignature(),
                        functionCode + funcbody.getFunctionImpl());


            case ARG:
                String argtype = root.nstype;
                if (argtype.equals("string")) argtype = "char *";
                return new CStringBuilder(argtype + " " + root.ID);

            case RETURN:
                String preReturn = "";

                for (String s : root.frees) {
                    preReturn += s;
                }
                CStringBuilder cb = recursive(root.body);
                String returnstatement = "return " + cb.getCode() + ending;
                return new CStringBuilder(cb.getPre(), preReturn+returnstatement, "", cb.getSignature(), cb.getFunctionImpl());

            case CALL:
                String callname = root.ID;
                String callargs = "(";
                String precall = "";
                String postcall = "";
                for (int i = root.args.size() - 1; i >= 0; i--) {
                    Node c = root.args.get(i);
                    CStringBuilder callargcb = recursive(c);
                    callargs += callargcb.getCode() + ",";
                    precall += callargcb.getPre();
                    postcall += callargcb.getPost();
                }
                if (root.args.size() > 0) callargs = callargs.substring(0, callargs.length() - 1);
                callargs += ")";
                String callcode = callname + callargs + ending;
                return new CStringBuilder(precall, callcode, postcall, "", "");

            case IF:
                CStringBuilder condbuilder = recursive(root.cond);
                CStringBuilder bodybuilder = recursive(root.body);
                String cond = "(" + condbuilder.getCode() + ")";
                String ifbody = bodybuilder.getCode();

                String elsebody = "";
                if (root.elsebody != null) {
                    CStringBuilder elsebuilder = recursive(root.elsebody);
                    elsebody = "else" + elsebuilder.getCode();
                }

                String ifcode = "if" + cond + ifbody + elsebody;
                return new CStringBuilder(ifcode);
            case WHILE:
                CStringBuilder whilebodyb = recursive(root.body);
                CStringBuilder whilecondb = recursive(root.cond);
                String whilecond = "(" + whilecondb.getCode() + ")";
                String whilebody = whilebodyb.getCode();

                String finalwhile = "while " + whilecond + whilebody;
                return new CStringBuilder(finalwhile);
        }
        return null;
    }

    public CStringBuilder merge(CStringBuilder a, CStringBuilder b) {
        return new CStringBuilder(
                a.getPre() + b.getPre(),
                a.getCode() + b.getCode(),
                a.getPost() + b.getPost(),
                a.getSignature() + b.getSignature(),
                a.getFunctionImpl() + b.getFunctionImpl());
    }


    public Node semanticAdjustment(Node root, Node parent, boolean comma, int level) {
        switch (root.type) {
            case BINOP:
                root.value = semanticAdjustment(root.value,root, comma, level);
                root.nstype = root.value.nstype;
                break;
            case VALUE:
                if (root.nstype == null) {
                    root.nstype = getType(root.text, level);
                }
                break;
            case ASSIGN:
                root.shouldComma = true;
                if (root.body.type == Type.FUNCTION) {
                    root.body.ID = root.ID;
                    root.fundecl = true;
                    root.shouldComma = false;
                }

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                if (getType(root.ID, level) != null) {
                    root.reassignment = true;
                }

                root.body = semanticAdjustment(root.body,root, comma, level);
                if (root.nstype == null) root.nstype = root.body.nstype;
                pushType(root.ID, root.nstype, level);

                if (root.nstype.equals("string")) {
                    if (parent.frees == null) {
                        parent.frees = new LinkedList<>();
                    }
                    parent.frees.add("free("+root.ID+");\n");
                }

                break;
            case INCOP:

                //a += b | a = a + b
                root.type = Type.ASSIGN;
                root.nstype = getType(root.ID, level);
                root.shouldComma = true;
                root.reassignment = true;

                //construct new body
                Node body = new Node(Type.BINOP);
                Node selfvalue = new Node(Type.VALUE);
                selfvalue.text = root.ID;
                selfvalue.nstype = root.nstype;
                body.value = selfvalue;
                body.sign = root.sign;
                body.body = root.body;
                root.body = body;
                root.body = semanticAdjustment(root.body,root,comma,level);
                root.body.nstype = root.nstype;

                //variable already contructed. already freed
                break;

            case IF:
                root.body = semanticAdjustment(root.body,root, comma, level);
                root.nstype = root.body.nstype;
                if (root.elsebody != null) {
                    root.elsebody = semanticAdjustment(root.elsebody,root, comma, level);
                }
                break;

            case WHILE:
                root.body = semanticAdjustment(root.body,root, comma, level);
                root.nstype = root.body.nstype;
                if (root.cond != null) {
                    root.cond = semanticAdjustment(root.cond,root, comma, level);
                } else {
                    Node one = new Node(Type.BINOP);
                    Node oneValue = new Node(Type.VALUE);
                    oneValue.text = "1";
                    oneValue.nstype = "int";
                    one.value = oneValue;
                    root.cond = one;
                }
                break;
            case BLOCK:
                ArrayList<Node> oldchildren = root.children;
                ArrayList<Node> l = new ArrayList<>();
                root.children = l;
                boolean foundReturn = false;
                for (Node n : oldchildren) {
                    Node k = semanticAdjustment(n,root, false, level + 1);
                    root.children.add(k);
                    if (k.type == Type.RETURN) {
                        foundReturn = true;
                        root.nstype = k.nstype;
                    }
                }
                if (!foundReturn) root.nstype = "void";

                break;
            case CALL:
                root.nstype = getType(root.ID, level);
                root.shouldComma = true;
                if (comma) root.shouldComma = false;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                ArrayList<Node> arglist = new ArrayList<>();
                for (Node arg : root.args) {
                    Node newarg = semanticAdjustment(arg,root, comma, level);
                    arglist.add(newarg);
                }
                root.args = arglist;

                break;
            case FUNCTION:
                for (Node n : root.args) {
                    pushType(n.ID, n.nstype, level + 1);
                }

                if (root.body.type != Type.BLOCK) {

                    Node block = new Node(Type.BLOCK);
                    ArrayList<Node> children = new ArrayList<>();


                    //does statement return something? yes no
                    if (root.body.type == Type.CALL || root.body.type == Type.BINOP) {
                        Node retnode = new Node(Type.RETURN);
                        retnode.body = semanticAdjustment(root.body,root, comma, level + 1);
                        children.add(retnode);
                    } else {
                        children.add(semanticAdjustment(root.body,root, comma, level + 1));
                    }

                    block.children = children;
                    root.body = block;
                }

                root.body = semanticAdjustment(root.body,root, comma, level);//body=block, block will inc level

                if (root.nstype == null) root.nstype = root.body.nstype;
                pushType(root.ID,root.nstype,level);
                break;
            case RETURN:
                root.shouldComma = true;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                //return variable? no? extract to variable:
                //!(root.body.type == Type.BINOP && root.body.body == null)
                if (root.body.type != Type.BINOP || root.body.body != null) {
                    Node extractAssign = new Node(Type.ASSIGN);
                    extractAssign.ID = generateRandomName();
                    extractAssign.body = semanticAdjustment(root.body,root, comma, level);
                    extractAssign.nstype = extractAssign.body.nstype;
                    pushType(extractAssign.ID,extractAssign.nstype,level);
                    parent.children.add(extractAssign);

                    //new return
                    Node newbody = new Node(Type.BINOP);
                    Node newvalue = new Node(Type.VALUE);
                    newvalue.text = extractAssign.ID;
                    newbody.value = newvalue;
                    newbody.nstype = extractAssign.nstype;
                    root.body = newbody;
                    root.nstype = extractAssign.nstype;
                } else {
                    root.body = semanticAdjustment(root.body,root,comma,level);
                    root.nstype = root.body.nstype;
                }

                //free case prints block frees
                root.frees = parent.frees;
                int idx = root.frees.indexOf("free("+root.body.value.text+");\n");
                root.frees.remove(idx);

                break;

            default:
                if (root.body != null) {
                    root.body = semanticAdjustment(root.body,root, comma, level);
                    if (root.nstype == null) {
                        root.nstype = root.body.nstype;
                    }
                }
                if (root.children != null) {
                    ArrayList<Node> newChildren = new ArrayList<>();
                    for (Node c : root.children) {
                        newChildren.add(semanticAdjustment(c,root, false, level));
                    }
                }
                break;
        }

        return root;
    }
}
