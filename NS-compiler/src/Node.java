public class Node {
    public Type type;
    public String text;
    public String ID;
    public Node body;
    public Node cond;

    public Node(Type type) {
        this.type = type;
    }
}
