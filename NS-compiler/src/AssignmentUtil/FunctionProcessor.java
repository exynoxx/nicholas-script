package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class FunctionProcessor {

    Pattern functionDecleration;
    boolean debug;
    Compiler compiler;

    public FunctionProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        functionDecleration = Pattern.compile("^\\s*func\\s*\\[(.*)\\]\\s*:?\\s*(\\w+)?\\s*\\{\\s*(.*)\\s*\\}");
    }

    public String convert(String name, String s) {

        Matcher matcher = functionDecleration.matcher(s);
        if (matcher.find()) {
            if (debug) System.out.println("---- function decleration");
            return functionDecleration(name, matcher); //variableValueType called inside function
        }

        return null;
    }

    private String functionDecleration(String name, Matcher matcher) {

        String args = matcher.group(1);
        String returnType = matcher.group(2);
        String body = matcher.group(3);

        if (returnType == null) {
            returnType = "void";
        }

        compiler.increaseScopeLevel();
        String translatedBody = compiler.tokenize(body);
        //translatedBody += compiler.getFreeStrings();

        String line = returnType + " " + name + "(" + args + ") {\n";
        line += translatedBody;
        line += "\n}";
        return line;
    }
}
