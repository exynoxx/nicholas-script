import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;
import org.antlr.v4.runtime.tree.ParseTreeWalker;

public class Compiler extends GrammarBaseVisitor<Node> {


    @Override
    public Node visitStart(GrammarParser.StartContext ctx) {
        return super.visitStart(ctx);
    }

    @Override
    public Node visitProgram(GrammarParser.ProgramContext ctx) {
        return super.visitProgram(ctx);
    }

    @Override
    public Node visitStatement(GrammarParser.StatementContext ctx) {
        return super.visitStatement(ctx);
    }

    @Override
    public Node visitBinop(GrammarParser.BinopContext ctx) {
        return super.visitBinop(ctx);
    }

    @Override
    public Node visitAssign(GrammarParser.AssignContext ctx) {
        return super.visitAssign(ctx);
    }

    @Override
    public Node visitIfstatement(GrammarParser.IfstatementContext ctx) {
        return super.visitIfstatement(ctx);
    }

    @Override
    public Node visitBlock(GrammarParser.BlockContext ctx) {
        return super.visitBlock(ctx);
    }

    public static void main(String[] args)  {
        String input = "var a = 5; if (5 > 2) {var b = 2+4-3;};";

        CharStream stream = new ANTLRInputStream(input);
        GrammarLexer lexer = new GrammarLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        GrammarParser parser = new GrammarParser(tokens);

        ParseTree tree = parser.start();
        ParseTreeWalker walker = new ParseTreeWalker();
        Compiler cp = new Compiler();
        walker.walk(cp,tree);
    }

}
