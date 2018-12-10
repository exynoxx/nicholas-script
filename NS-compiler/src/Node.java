import java.util.ArrayList;

public class Node {
    public Type type;
    public String text;
    public String ID;
    public String nstype;
    public Node body;
    public Node cond;
    public ArrayList<Node> children;
    public ArrayList<Node> args;
    public boolean fundecl;

    public Node(Type type) {
        this.type = type;
    }
}
