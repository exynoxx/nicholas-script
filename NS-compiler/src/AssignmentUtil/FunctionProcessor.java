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

        String[] argsList = args.split(",");
        args = args.replaceAll("string", "nstring *");

        compiler.increaseScopeLevel();
        String translatedBody = compiler.tokenize(body);

        //for each variable v in the arguments replace the variable in the body with v->data
        /*
        for (String x : argsList) {
            if (x.contains("string")) {
                String v = x.split(" ")[1];
                translatedBody = translatedBody.replaceAll(v, v+"->data");
            }
        }*/

        String line = returnType + " " + name + "(" + args + ") {\n";
        line += translatedBody;
        line += "\n}";
        return line;
    }
}
