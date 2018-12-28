public class CStringBuilder {
    String pre;
    String code;
    String post;
    String signature;
    String functionImpl;

    public String getPre() {
        return pre;
    }

    public String getCode() {
        return code;
    }

    public String getPost() {
        return post;
    }

    public String getPreCodePost() {
        return pre + code + post;
    }

    public String getSignature() {
        return signature;
    }

    public String getFunctionImpl() {
        return functionImpl;
    }

    public CStringBuilder(String pre, String code, String post, String signature, String functionImpl) {
        this.pre = pre;
        this.code = code;
        this.post = post;
        this.signature = signature;
        this.functionImpl = functionImpl;
    }

    public CStringBuilder(String code) {
        this.code = code;

        this.pre = "";
        this.post = "";
        this.signature = "";
        this.functionImpl = "";
    }

    public CStringBuilder() {
        this.code = "";
        this.pre = "";
        this.post = "";
        this.signature = "";
        this.functionImpl = "";
    }
}