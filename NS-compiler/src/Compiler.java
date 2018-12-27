import org.antlr.v4.runtime.ANTLRInputStream;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.CommonTokenStream;
import org.antlr.v4.runtime.tree.ParseTree;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class Compiler {

    public static String extractCCode(String s) {
        if (s.indexOf(":CBLOCKBEGIN:") == -1){
            return "";
        }

        Pattern p = Pattern.compile(":CBLOCKBEGIN:(([^\\n]*(\\n+))+):CBLOCKEND:");
        Matcher m = p.matcher(s);
        String ccode = "";
        while (m.find()) {
            ccode += m.group(1);
        }
        return ccode;
    }

    public static String removeCCodeBlock (String s) {
        return s.replaceAll(":CBLOCKBEGIN:(?:[^\\n]*(\\n+))+:CBLOCKEND:", "");
    }

    public static String readFile(String path) throws IOException {
        byte[] encoded = Files.readAllBytes(Paths.get(path));
        return new String(encoded);
    }
    public static void writeFile (String path, String source) throws IOException {
        Path file = Paths.get(path);
        Files.write(file,source.getBytes());
    }

    public static String extractImports (String s) throws IOException {

        Pattern pattern = Pattern.compile("import\\s+\"(.+)\";");
        Matcher m = pattern.matcher(s);

        if (!m.find()) {
            return s;
        }
        m.reset();
        String ret = "";
        while (m.find()) {
            String url = m.group(1);
            String file = readFile(url);
            ret = m.replaceAll(file);
        }
        return ret;
    }

    public static void main(String[] args) throws IOException {
        String inputnum = "2";
        String input = readFile("src/examples/"+inputnum+".ns");

        input = extractImports(input);
        String cCode = extractCCode(input);
        input = removeCCodeBlock(input);

        CharStream stream = new ANTLRInputStream(input);
        GrammarLexer lexer = new GrammarLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lexer);
        GrammarParser parser = new GrammarParser(tokens);

        ParseTree tree = parser.start();
        NSGrammarVisitor cp = new NSGrammarVisitor();
        Node root = cp.visit(tree);
        //cp.prettyPrint(root, 0,4);

        BackendC out = new BackendC(cCode);
        String output = out.gen(root);
        System.out.println(output);
        writeFile("src/out/"+inputnum+".c",output);


    }

    /*

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
    */


}
