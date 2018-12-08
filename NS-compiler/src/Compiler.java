import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;

import java.util.ArrayList;
import java.util.List;

public class Compiler extends GrammarBaseVisitor<Node> {

    @Override
    public Node visitStart(GrammarParser.StartContext ctx) {
        return this.visit(ctx.program());
    }

    @Override
    public Node visitProgram(GrammarParser.ProgramContext ctx) {
        Node n = new Node(Type.PROGRAM);

        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            n.children.add(this.visit(c));
        }
        return n;
    }

    @Override
    public Node visitIfifstatement(GrammarParser.IfifstatementContext ctx) {
        return this.visit(ctx.ifstatement());
    }

    @Override
    public Node visitIfstatement(GrammarParser.IfstatementContext ctx) {
        Node n = new Node(Type.IF);
        n.cond = this.visit(ctx.binop());
        n.body = this.visit(ctx.block());
        return n;
    }

    @Override
    public Node visitSign(GrammarParser.SignContext ctx) {
        Node n = new Node(Type.SIGN);
        n.text = ctx.getText();
        return n;
    }

    @Override
    public Node visitAssignstatement(GrammarParser.AssignstatementContext ctx) {
        return this.visit(ctx.assign());
    }

    @Override
    public Node visitAssign(GrammarParser.AssignContext ctx) {
        Node n = new Node(Type.ASSIGN);
        n.body = this.visit(ctx.binop());
        n.ID = ctx.ID().toString();
        return n;
    }

    @Override
    public Node visitBinop(GrammarParser.BinopContext ctx) {
        Node n = new Node(Type.BINOP);
        n.text = ctx.getText();
        return n;
    }

    @Override
    public Node visitBlock(GrammarParser.BlockContext ctx) {
        Node n = new Node(Type.BLOCK);

        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            n.children.add(this.visit(c));
        }
        return n;
    }

    @Override
    public Node visitValue(GrammarParser.ValueContext ctx) {
        Node n = new Node(Type.VALUE);
        n.text = ctx.getText();
        return n;
    }

    public void printSpace(int depth) {
        for (int i = 0; i < depth; i++) {
            System.out.print("-");
        }
    }

    public void prettyPrint(Node root, int depth, int inc) {
        switch (root.type) {
            case PROGRAM:
                for (Node n : root.children) {
                    prettyPrint(n, depth + inc,inc);
                }
                break;
            case BLOCK:
                printSpace(depth);
                System.out.println("BLOCK");
                for (Node n : root.children) {
                    prettyPrint(n, depth + inc,inc);
                }
                break;
            case IF:
                printSpace(depth);
                System.out.println("IF");
                prettyPrint(root.cond, depth + inc,inc);
                prettyPrint(root.body, depth + inc,inc);
                break;

            case ASSIGN:
                printSpace(depth);
                System.out.println("ASSIGN");
                printSpace(depth);
                System.out.println("ID: " + root.ID);
                prettyPrint(root.body, depth + inc,inc);
                break;

            case BINOP:
                printSpace(depth);
                System.out.println("BINOP");
                printSpace(depth+inc);
                System.out.println("value: " + root.text);
                break;


        }
    }

    public static void main(String[] args) {
        String input = "var a = 5; if (6 > a) {var b = 2+a-3;var c = \"string  hello world\";};";

        CharStream stream = new ANTLRInputStream(input);
        GrammarLexer lexer = new GrammarLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        GrammarParser parser = new GrammarParser(tokens);

        ParseTree tree = parser.start();
        Compiler cp = new Compiler();
        Node root = cp.visit(tree);
        cp.prettyPrint(root, 0,4);

    }

}
