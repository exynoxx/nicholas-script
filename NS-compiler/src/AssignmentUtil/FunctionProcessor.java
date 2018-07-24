package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class FunctionProcessor {

    Pattern functionDecleration;
    boolean debug;
    Compiler compiler;
    Matcher m;

    public FunctionProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        functionDecleration = Pattern.compile("^\\s*func\\s*\\[(.*)\\]\\s*:?\\s*(\\w+)?\\s*\\{\\s*(.*)\\s*\\}");
    }

    public boolean test (String s) {
        m = functionDecleration.matcher(s);
        return m.find();
    }

    public String convert(String name, String s) {
        if (debug) System.out.println("---- function decleration");
        return functionDecleration(name); //variableValueType called inside function
    }

    private String functionDecleration(String name) {

        String args = m.group(1);
        String returnType = m.group(2);
        String body = m.group(3);
        boolean shouldAddFrees = false;

        if (returnType == null) {
            returnType = "void";
            shouldAddFrees = true;
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
        if (shouldAddFrees) line += compiler.getFreeStrings();
        line += "}\n\n";
        return line;
    }
}
