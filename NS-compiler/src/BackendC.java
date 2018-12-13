import java.util.ArrayList;
import java.util.HashMap;

public class BackendC {

    HashMap<String, String> typesHM = new HashMap<>();

    public String gen(Node root) {
        root = semanticAdjustment(root);
        CodeBuilder cb = recursive(root, 0);
        String ret = "//signatures \n" + cb.getSignature() + "\n" +
                "//functions \n" + cb.getFunctionImpl() + "\n\n" +
                cb.getCode();
        return ret;
    }

    public CodeBuilder recursive(Node root, int level) {
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
                String line = type + " " + name + " = " + body.getCode() + ";\n";
                if (root.fundecl) {
                    line = "";
                }
                return new CodeBuilder(body.getPre(), line, body.getPost(), body.getSignature(), body.getFunctionImpl());


            case BINOP:
                if (root.body == null) {
                    //leaf
                    return recursive(root.value, level + 1);
                } else {
                    CodeBuilder cbbody = recursive(root.body, level + 1);
                    CodeBuilder cbsign = recursive(root.sign, level + 1);
                    CodeBuilder cbvalue = recursive(root.value, level + 1);

                    CodeBuilder cbret1 = merge(cbvalue, cbsign);
                    CodeBuilder cbret2 = merge(cbret1, cbbody);
                    return cbret2;
                }
            case SIGN:
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
                String signature = root.nstype + " " + root.ID + args + ";\n";
                String functionCode = root.nstype + " " + root.ID + args + cbody;
                return new CodeBuilder(
                        "",
                        "",
                        "",
                        signature + funcbody.getSignature(),
                        functionCode + funcbody.getFunctionImpl());


            case ARG:
                return new CodeBuilder(root.nstype + " " + root.ID);

            case RETURN:
                CodeBuilder cb = recursive(root.body, level + 1);
                String returnstatement = "return " + cb.getCode() + ";\n";
                return new CodeBuilder(returnstatement);

            case CALL:
                String callname = root.ID;
                String callargs = "(";
                for (int i = root.args.size() - 1; i >= 0; i--) {
                    Node c = root.args.get(i);
                    CodeBuilder callargcb = recursive(c, level + 1);
                    callargs += callargcb.getCode() + ",";
                }
                if (root.args.size() > 0) callargs = callargs.substring(0, callargs.length() - 1);
                callargs += ")";
                String callcode = callname + callargs + ";\n";
                return new CodeBuilder(callcode);
            case VALUE:
                return new CodeBuilder(root.text);

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

    public Node semanticAdjustment(Node root) {
        switch (root.type) {
            case BINOP:
                root.value = semanticAdjustment(root.value);
                root.nstype = root.value.nstype;
                break;
            case VALUE:
                if (root.nstype == null) {
                    root.nstype = typesHM.get(root.text);
                }
                break;
            case ASSIGN:
                if (root.body.type == Type.FUNCTION) {
                    root.body.ID = root.ID;
                    root.fundecl = true;
                }

                root.body = semanticAdjustment(root.body);
                if (root.nstype == null) root.nstype = root.body.nstype;
                typesHM.put(root.ID, root.nstype);

                break;
            case BLOCK:
                ArrayList<Node> l = new ArrayList<>();
                for (Node n : root.children) {
                    Node k = semanticAdjustment(n);
                    l.add(k);
                    if (k.type == Type.RETURN) {
                        root.nstype = k.nstype;
                    }
                }
                root.children = l;
                break;
            case CALL:
                root.nstype = typesHM.get(root.ID);
                break;
            case FUNCTION:
                root.body = semanticAdjustment(root.body);
                if (root.nstype == null) root.nstype = root.body.nstype;
                typesHM.put(root.ID,root.nstype);
                break;

            default:
                if (root.body != null) {
                    root.body = semanticAdjustment(root.body);
                    if (root.nstype == null) {
                        root.nstype = root.body.nstype;
                    }
                }
                if (root.children != null) {
                    ArrayList<Node> newChildren = new ArrayList<>();
                    for (Node c : root.children) {
                        newChildren.add(semanticAdjustment(c));
                    }
                }
                break;
        }

        return root;
    }
}
