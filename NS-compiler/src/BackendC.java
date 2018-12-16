import java.util.*;

public class BackendC {

    HashMap<Integer,HashMap<String,String>> typesHM = new HashMap<Integer, HashMap<String, String>>();
    Random random = new Random();

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
        root = semanticAdjustment(root, false,0);
        CodeBuilder cb = recursive(root);
        String ret = "//signatures \n" + cb.getSignature() + "\n" +
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

    public void pushType (String name, String type, int level) {

        if (typesHM.get(level) == null) {
            typesHM.put(level,new HashMap<>());
        }

        HashMap<String,String> tmp = typesHM.get(level);
        tmp.put(name,type);
    }

    public String getType (String name,int level) {
        String ret = null;
        while (level >= 0){
            if (ret != null) {
                break;
            } else {
                if (typesHM.get(level) == null) return null;
                ret = typesHM.get(level).get(name);
            }
            level--;
        }
        return ret;
    }

    public void resetLevel (int level) {
        typesHM.get(level).clear();
    }


    public CodeBuilder recursive(Node root) {
        String ending = (root.shouldComma) ? ";\n" : "";
        switch (root.type) {
            case PROGRAM:
                CodeBuilder ret = new CodeBuilder("", "", "", "", "");
                String retCode = "";
                String postCode = "";
                for (Node c : root.children) {
                    CodeBuilder cb = recursive(c);
                    ret = merge(ret, cb);
                    retCode += cb.getPre()+cb.getCode();
                    postCode += cb.getPost();
                }

                String code = "void main () {\n";
                code += retCode+postCode;
                code += "}\n";
                return new CodeBuilder("", code, "", ret.getSignature(), ret.getFunctionImpl());


            case ASSIGN:
                String name = root.ID;
                String type = root.nstype + " ";

                CodeBuilder body = recursive(root.body);
                if (root.body.nstype.equals("string")) type = "char *";

                if (root.reassignment) {
                    type = "";
                }

                String line = type + name + " = " + body.getCode() + ending;

                if (root.fundecl) {
                    line = "";
                }
                return new CodeBuilder(body.getPre(), line, body.getPost(), body.getSignature(), body.getFunctionImpl());


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
                            list.add( nodeBody.value.text);

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
                            scopy += "strcat("+rname2+", "+s+");\n";
                        }
                        String finalline = rname2;
                        String freeline = "free (" + rname2 + ");\n";
                        return new CodeBuilder(finalsizeline+salloc+scopy,finalline,freeline,"","");

                    } else {
                        CodeBuilder cbbody = recursive(root.body);
                        CodeBuilder cbsign = recursive(root.sign);
                        CodeBuilder cbvalue = recursive(root.value);

                        CodeBuilder cbret1 = merge(cbvalue, cbsign);
                        CodeBuilder cbret2 = merge(cbret1, cbbody);
                        return cbret2;
                    }


                }
            case SIGN:
                return new CodeBuilder(root.text);

            case VALUE:

                if (root.nstype != null && root.nstype.equals("string")) {
                    String rname = generateRandomName();
                    int ssize = root.text.length() - 2;
                    String salloc = "char *" + rname + " = (char *) malloc (" + ssize + ");\n";
                    String scopy = "strcpy(" + rname + ", " + root.text + ");\n";
                    String pre = salloc + scopy;
                    String scode = rname;
                    String post = "free (" + rname + ");\n";
                    return new CodeBuilder(pre, scode, post, "", "");
                }

                return new CodeBuilder(root.text);

            case BLOCK:
                CodeBuilder block = new CodeBuilder("", "", "", "", "");
                String blockCode = "";
                String postBlockCode = "";
                for (Node c : root.children) {
                    CodeBuilder cb = recursive(c);
                    block = merge(block, cb);
                    blockCode += cb.getPre()+cb.getCode();
                    postBlockCode += cb.getPost();
                }

                String codeblock = "{\n";
                codeblock += blockCode+postBlockCode;
                codeblock += "}\n";
                return new CodeBuilder("", codeblock, "", block.getSignature(), block.getFunctionImpl());

            case FUNCTION:

                String args = "(";
                for (Node c : root.args) {
                    CodeBuilder cb = recursive(c);
                    args += cb.getCode() + ",";
                }
                if (root.args.size() > 0) args = args.substring(0, args.length() - 1);
                args += ")";

                CodeBuilder funcbody = recursive(root.body);
                String cbody = funcbody.getPreCodePost();
                String ftype = root.nstype;
                if (ftype.equals("string")) ftype = "char *";
                String signature = ftype + " " + root.ID + args + ";\n";
                String functionCode = ftype + " " + root.ID + args + cbody;
                return new CodeBuilder(
                        "",
                        "",
                        "",
                        signature + funcbody.getSignature(),
                        functionCode + funcbody.getFunctionImpl());


            case ARG:
                String argtype = root.nstype;
                if (argtype.equals("string")) argtype = "char *";
                return new CodeBuilder(argtype + " " + root.ID);

            case RETURN:
                CodeBuilder cb = recursive(root.body);
                String returnstatement = "return " + cb.getCode() + ending;
                return new CodeBuilder(cb.getPre(),returnstatement,"",cb.getSignature(),cb.getFunctionImpl());

            case CALL:
                String callname = root.ID;
                String callargs = "(";
                String precall = "";
                String postcall = "";
                for (int i = root.args.size() - 1; i >= 0; i--) {
                    Node c = root.args.get(i);
                    CodeBuilder callargcb = recursive(c);
                    callargs += callargcb.getCode() + ",";
                    precall += callargcb.getPre();
                    postcall += callargcb.getPost();
                }
                if (root.args.size() > 0) callargs = callargs.substring(0, callargs.length() - 1);
                callargs += ")";
                String callcode = callname + callargs + ending;
                return new CodeBuilder(precall,callcode,postcall,"","");

            case IF:
                CodeBuilder condbuilder = recursive(root.cond);
                CodeBuilder bodybuilder = recursive(root.body);
                String cond = "(" + condbuilder.getCode() + ")";
                String ifbody = bodybuilder.getCode();
                String ifcode = "if" + cond + ifbody;
                return new CodeBuilder(ifcode);
        }
        return null;
    }

    public CodeBuilder merge(CodeBuilder a, CodeBuilder b) {
        return new CodeBuilder(
                a.getPre() + b.getPre(),
                a.getCode() + b.getCode(),
                a.getPost() + b.getPost(),
                a.getSignature() + b.getSignature(),
                a.getFunctionImpl() + b.getFunctionImpl());
    }

    public Node semanticAdjustment(Node root, boolean comma,int level) {
        switch (root.type) {
            case BINOP:
                root.value = semanticAdjustment(root.value, comma,level);
                root.nstype = root.value.nstype;
                break;
            case VALUE:
                if (root.nstype == null) {
                    root.nstype = getType(root.text,level);
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

                if (getType(root.ID,level) != null) {
                    root.reassignment = true;
                }

                root.body = semanticAdjustment(root.body, comma,level);
                if (root.nstype == null) root.nstype = root.body.nstype;
                pushType(root.ID,root.nstype,level);

                break;
            case BLOCK:
                ArrayList<Node> l = new ArrayList<>();
                boolean foundReturn = false;
                for (Node n : root.children) {
                    Node k = semanticAdjustment(n, false,level+1);
                    l.add(k);
                    if (k.type == Type.RETURN) {
                        foundReturn = true;
                        root.nstype = k.nstype;
                    }
                }
                if (!foundReturn) root.nstype = "void";
                root.children = l;
                break;
            case CALL:
                root.nstype = getType(root.ID,level);
                root.shouldComma = true;
                if (comma) root.shouldComma = false;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                ArrayList<Node> arglist = new ArrayList<>();
                for (Node arg : root.args) {
                    Node newarg = semanticAdjustment(arg, comma,level);
                    arglist.add(newarg);
                }
                root.args = arglist;

                break;
            case FUNCTION:
                for (Node n : root.args) {
                    pushType(n.ID,n.nstype,level+1);
                }

                if (root.body.type != Type.BLOCK) {

                    Node block = new Node(Type.BLOCK);
                    ArrayList<Node> children = new ArrayList<>();
                    children.add(semanticAdjustment(root.body, comma,level+1));
                    block.children = children;

                    root.body = block;
                }

                root.body = semanticAdjustment(root.body, comma,level);//body=block, block will inc level

                if (root.nstype == null) root.nstype = root.body.nstype;
                //pushType(root.ID,root.nstype,level);
                break;
            case RETURN:
                root.shouldComma = true;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                root.body = semanticAdjustment(root.body, comma,level);
                root.nstype = root.body.nstype;
                break;

            default:
                if (root.body != null) {
                    root.body = semanticAdjustment(root.body, comma,level);
                    if (root.nstype == null) {
                        root.nstype = root.body.nstype;
                    }
                }
                if (root.children != null) {
                    ArrayList<Node> newChildren = new ArrayList<>();
                    for (Node c : root.children) {
                        newChildren.add(semanticAdjustment(c, false,level));
                    }
                }
                break;
        }

        return root;
    }
}
