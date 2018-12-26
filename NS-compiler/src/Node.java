import java.util.ArrayList;

public class Node {
    public Type type;
    public String text;
    public String ID;
    public String nstype;
    public Node body;
    public Node elsebody;
    public Node cond;
    public Node sign;
    public Node value;
    public ArrayList<Node> children;
    public ArrayList<Node> args;
    public boolean fundecl;
    public boolean shouldComma;
    public boolean reassignment;

    public Node(Type type) {
        this.type = type;
    }
}
