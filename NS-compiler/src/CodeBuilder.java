public class CodeBuilder {
    String pre;
    String code;
    String post;

    public String getPre() {
        return pre;
    }

    public String getCode() {
        return code;
    }

    public String getPost() {
        return post;
    }

    public CodeBuilder(String pre, String code, String pos) {

        this.pre = pre;
        this.code = code;
        this.post = pos;
    }
}
