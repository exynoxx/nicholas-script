import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;

public class Compiler extends GrammarBaseVisitor<Node> {

    @Override
    public Node visitStart(GrammarParser.StartContext ctx) {
        return this.visit(ctx.program());
    }

    public Node visitProgram(GrammarParser.ProgramContext ctx) {
        Node n = new Node(Type.PROGRAM);

        ArrayList<Node> children = new ArrayList<>();

        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            children.add(this.visit(c));
        }
        n.children = children;
        return n;
    }

    //statement ##############################

    //assign ##########################
    @Override
    public Node visitAssigneval(GrammarParser.AssignevalContext ctx) {
        Node n = new Node(Type.ASSIGN);
        n.body = this.visit(ctx.eval());
        n.ID = ctx.ID().toString();
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : null;
        return n;
    }
    @Override
    public Node visitAssignfunction(GrammarParser.AssignfunctionContext ctx) {
        Node n = new Node(Type.ASSIGN);
        n.body = this.visit(ctx.function());
        n.ID = ctx.ID().toString();
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : null;

        return n;
    }

    //iff ##########################################
    @Override
    public Node visitIff(GrammarParser.IffContext ctx) {
        Node n = new Node(Type.IF);
        n.cond = this.visit(ctx.binop());
        n.body = this.visit(ctx.block());
        return n;
    }


    //return ####################################3
    @Override
    public Node visitReturnn(GrammarParser.ReturnnContext ctx) {
        Node n = new Node(Type.RETURN);
        n.body = this.visit(ctx.eval());
        return n;
    }


    //call ##############################################
    @Override
    public Node visitCall(GrammarParser.CallContext ctx) {
        Node n = new Node(Type.CALL);

        ArrayList<Node> args = new ArrayList<>();
        List<GrammarParser.CallargContext> l = ctx.callarg();
        for (int i = l.size()-1;i >= 0; i--) {
            GrammarParser.CallargContext arg = l.get(i);
            args.add(this.visit(arg));
        }
        n.args = args;
        n.ID = ctx.ID().toString();
        return n;
    }

    //callarg #############################
    @Override
    public Node visitCallargvalue(GrammarParser.CallargvalueContext ctx) {
        return this.visit(ctx.value());
    }

    @Override
    public Node visitCallargeval(GrammarParser.CallargevalContext ctx) {
        return this.visit(ctx.eval());
    }

    @Override
    public Node visitCallargcall(GrammarParser.CallargcallContext ctx) {
        return this.visit(ctx.call());
    }
//eval #############################################

    //binop #########################################
    @Override
    public Node visitBinopvalue(GrammarParser.BinopvalueContext ctx) {
        Node n = new Node(Type.BINOP);
        n.value = this.visit(ctx.value());
        return n;
    }
    @Override
    public Node visitBinopbinop(GrammarParser.BinopbinopContext ctx) {
        Node n = new Node(Type.BINOP);
        n.body = this.visit(ctx.binop());
        n.sign = this.visit(ctx.sign());
        n.value = this.visit(ctx.value());
        return n;
    }
    @Override
    public Node visitSign(GrammarParser.SignContext ctx) {
        Node n = new Node(Type.SIGN);
        n.text = ctx.getText();
        return n;
    }

    //function ####################################################
    @Override
    public Node visitFunction(GrammarParser.FunctionContext ctx) {
        Node n = new Node(Type.FUNCTION);

        ArrayList<Node> args = new ArrayList<>();
        List<GrammarParser.ArgContext> l = ctx.arg();
        for (GrammarParser.ArgContext arg : l) {
            args.add(this.visit(arg));
        }
        n.body = this.visit(ctx.fbody());
        try {
            n.nstype = ctx.TYPE().toString();
        } catch (Exception e) {}
        n.args = args;
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : null;
        return n;
    }

    @Override
    public Node visitArg(GrammarParser.ArgContext ctx) {
        Node n = new Node(Type.ARG);
        n.ID = ctx.ID().toString();
        n.nstype = ctx.TYPE().toString();
        return n;
    }

    //block ######################################################
    @Override
    public Node visitBlock(GrammarParser.BlockContext ctx) {
        Node n = new Node(Type.BLOCK);

        ArrayList<Node> children = new ArrayList<>();
        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            children.add(this.visit(c));
        }
        n.children = children;
        return n;
    }


    @Override
    public Node visitValueID(GrammarParser.ValueIDContext ctx) {
        Node n = new Node(Type.VALUE);
        n.text = ctx.getText();
        return n;
    }
    @Override
    public Node visitValueNUM(GrammarParser.ValueNUMContext ctx) {
        Node n = new Node(Type.VALUE);
        n.text = ctx.getText();
        n.nstype = "int";
        return n;
    }
    @Override
    public Node visitValueSTRING(GrammarParser.ValueSTRINGContext ctx) {
        Node n = new Node(Type.VALUE);
        n.text = ctx.getText();
        n.nstype = "string";
        return n;
    }
    //###################################

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

            case FUNCTION:
                printSpace(depth);
                System.out.println("FUNCTION");
                printSpace(depth);
                System.out.println("args:");
                for (Node i : root.args) {
                    printSpace(depth+inc);
                    System.out.println(i.ID + "(" + i.nstype + ")");
                }
                printSpace(depth);
                System.out.println("body:");
                prettyPrint(root.body,depth+inc,inc);
                break;

            case RETURN:
                printSpace(depth);
                System.out.println("RETURN");
                prettyPrint(root.body,depth+inc,inc);
                break;

            case CALL:
                printSpace(depth);
                System.out.println("CALL");
                printSpace(depth);
                System.out.println("ARGS:");
                for (Node i : root.args) {
                    printSpace(depth+inc);
                    System.out.println(i.text);
                }
        }
    }


    public static String readFile(String path) throws IOException {
        byte[] encoded = Files.readAllBytes(Paths.get(path));
        return new String(encoded);
    }

    public static void main(String[] args) throws IOException {
        //String input = "var f1 = (a:int,b:int) => {var f2 = (a:int) => {var a=1+4-2;return a;}; f2: 1;return f2: 2;};f1:500 0;";
        //String input = "var b:int = 2+a-3;";

        String input = readFile("src/examples/1.ns");


        CharStream stream = new ANTLRInputStream(input);
        GrammarLexer lexer = new GrammarLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        GrammarParser parser = new GrammarParser(tokens);

        ParseTree tree = parser.start();
        Compiler cp = new Compiler();
        Node root = cp.visit(tree);
        //cp.prettyPrint(root, 0,4);

        BackendC out = new BackendC();
        System.out.println(out.gen(root));

    }

}
