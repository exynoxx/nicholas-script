import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;
import org.antlr.v4.runtime.tree.ParseTreeWalker;

public class Compiler extends GrammarBaseListener{

    @Override
    public void enterProgram(GrammarParser.ProgramContext ctx) {
        System.out.println("enter program");
        super.enterProgram(ctx);
    }

    public static void main(String[] args)  {
        String input = "5+5-3;";

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
