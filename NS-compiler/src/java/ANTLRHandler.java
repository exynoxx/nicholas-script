package java;

import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;

public class ANTLRHandler {

    public Node buildNodeTree(String src) {

        CharStream stream = new ANTLRInputStream(src);
        GrammarLexer lexer = new GrammarLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        GrammarParser parser = new GrammarParser(tokens);

        ParseTree tree = parser.start();
        NSGrammarVisitor cp = new NSGrammarVisitor();
        Node root = cp.visit(tree);
        return root;
    }
}
