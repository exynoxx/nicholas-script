import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Random;

public class BackendC {

    HashMap<String, String> typesHM = new HashMap<>();
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
        root = semanticAdjustment(root, false);
        CodeBuilder cb = recursive(root, 0);
        String ret = "//signatures \n" + cb.getSignature() + "\n" +
                "//functions \n" + cb.getFunctionImpl() + "\n" +
                "//main \n" + cb.getCode();
        return ret;
    }

    public CodeBuilder recursive(Node root, int level) {
        String ending = (root.shouldComma) ? ";\n" : "";
        switch (root.type) {
            case PROGRAM:
                CodeBuilder ret = new CodeBuilder("", "", "", "", "");
                for (Node c : root.children) {
                    CodeBuilder cb = recursive(c, level + 1);
                    ret = merge(ret, cb);
                }

                String code = "void main () {\n";
                code += ret.getPreCodePost();
                code += "}\n";
                return new CodeBuilder("", code, "", ret.getSignature(), ret.getFunctionImpl());


            case ASSIGN:
                String name = root.ID;
                String type = root.nstype;

                CodeBuilder body = recursive(root.body, level + 1);
                if (root.body.nstype.equals("string")) type = "char *";
                String line = type + " " + name + " = " + body.getCode() + ending;



                if (root.fundecl) {
                    line = "";
                }
                return new CodeBuilder(body.getPre(), line, body.getPost(), body.getSignature(), body.getFunctionImpl());


            case BINOP:
                if (root.body == null) {
                    //leaf
                    return recursive(root.value, level + 1);
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
                        CodeBuilder cbbody = recursive(root.body, level + 1);
                        CodeBuilder cbsign = recursive(root.sign, level + 1);
                        CodeBuilder cbvalue = recursive(root.value, level + 1);

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
                for (Node c : root.children) {
                    CodeBuilder cb = recursive(c, level + 1);
                    block = merge(block, cb);
                }

                String codeblock = "{\n";
                codeblock += block.getPreCodePost();
                codeblock += "}\n";
                return new CodeBuilder("", codeblock, "", block.getSignature(), block.getFunctionImpl());

            case FUNCTION:

                String args = "(";
                for (Node c : root.args) {
                    CodeBuilder cb = recursive(c, level + 1);
                    args += cb.getCode() + ",";
                }
                if (root.args.size() > 0) args = args.substring(0, args.length() - 1);
                args += ")";

                CodeBuilder funcbody = recursive(root.body, level + 1);
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
                CodeBuilder cb = recursive(root.body, level + 1);
                String returnstatement = "return " + cb.getCode() + ending;
                return new CodeBuilder(cb.getPre(),returnstatement,"",cb.getSignature(),cb.getFunctionImpl());

            case CALL:
                String callname = root.ID;
                String callargs = "(";
                String precall = "";
                String postcall = "";
                for (int i = root.args.size() - 1; i >= 0; i--) {
                    Node c = root.args.get(i);
                    CodeBuilder callargcb = recursive(c, level + 1);
                    callargs += callargcb.getCode() + ",";
                    precall += callargcb.getPre();
                    postcall += callargcb.getPost();
                }
                if (root.args.size() > 0) callargs = callargs.substring(0, callargs.length() - 1);
                callargs += ")";
                String callcode = callname + callargs + ending;
                return new CodeBuilder(precall,callcode,postcall,"","");

            case IF:
                CodeBuilder condbuilder = recursive(root.cond, level + 1);
                CodeBuilder bodybuilder = recursive(root.body, level + 1);
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

    public Node semanticAdjustment(Node root, boolean comma) {
        switch (root.type) {
            case BINOP:
                root.value = semanticAdjustment(root.value, comma);
                root.nstype = root.value.nstype;
                break;
            case VALUE:
                if (root.nstype == null) {
                    root.nstype = typesHM.get(root.text);
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

                root.body = semanticAdjustment(root.body, comma);
                if (root.nstype == null) root.nstype = root.body.nstype;
                typesHM.put(root.ID, root.nstype);

                break;
            case BLOCK:
                ArrayList<Node> l = new ArrayList<>();
                for (Node n : root.children) {
                    Node k = semanticAdjustment(n, false);
                    l.add(k);
                    if (k.type == Type.RETURN) {
                        root.nstype = k.nstype;
                    }
                }
                root.children = l;
                break;
            case CALL:
                root.nstype = typesHM.get(root.ID);
                root.shouldComma = true;
                if (comma) root.shouldComma = false;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                ArrayList<Node> arglist = new ArrayList<>();
                for (Node arg : root.args) {
                    Node newarg = semanticAdjustment(arg, comma);
                    arglist.add(newarg);
                }
                root.args = arglist;

                break;
            case FUNCTION:
                for (Node n : root.args) {
                    typesHM.put(n.ID, n.nstype);
                }

                if (root.body.type != Type.BLOCK) {

                    Node block = new Node(Type.BLOCK);
                    ArrayList<Node> children = new ArrayList<>();
                    children.add(semanticAdjustment(root.body, comma));
                    block.children = children;

                    root.body = block;
                }
                root.body = semanticAdjustment(root.body, comma);

                if (root.nstype == null) root.nstype = root.body.nstype;
                typesHM.put(root.ID, root.nstype);
                break;
            case RETURN:
                root.shouldComma = true;

                if (comma) {
                    root.shouldComma = false;
                } else {
                    comma = true;
                }

                root.body = semanticAdjustment(root.body, comma);
                root.nstype = root.body.nstype;
                break;

            default:
                if (root.body != null) {
                    root.body = semanticAdjustment(root.body, comma);
                    if (root.nstype == null) {
                        root.nstype = root.body.nstype;
                    }
                }
                if (root.children != null) {
                    ArrayList<Node> newChildren = new ArrayList<>();
                    for (Node c : root.children) {
                        newChildren.add(semanticAdjustment(c, false));
                    }
                }
                break;
        }

        return root;
    }
}
