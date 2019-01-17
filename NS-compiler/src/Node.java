import java.util.ArrayList;

public class Node {
    public Type type;
    public String text;
    public String ID;
    public Node body;
    public Node elsebody;
    public Node cond;
    public String sign;
    public Node value;
    public ArrayList<Node> children;
    public ArrayList<Node> args;

    public Node(Type type) {
        this.type = type;
    }
}
