public class BackendC {
    public String gen (Node root) {
        CodeBuilder cb = recursive(root);
        String ret = cb.getPre()+cb.getCode()+cb.getPost();
        return ret;
    }

    public CodeBuilder recursive (Node root) {
        return null;
    }
}
