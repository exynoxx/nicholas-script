public class BackendC {

    public String gen(Node root) {
        CodeBuilder cb = recursive(root, 0);
        String ret = cb.getSignature() + cb.getFunctionImpl() + cb.getCode();
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
                return new CodeBuilder(body.getPre(),line,body.getPost(),body.getSignature(),body.getFunctionImpl());


            case BINOP:
                return new CodeBuilder(root.text);
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
}
