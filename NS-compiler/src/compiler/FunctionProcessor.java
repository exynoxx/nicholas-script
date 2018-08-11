package compiler;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class FunctionProcessor {

    Pattern functionDecleration;
    Matcher m;
    Box box;

    //TODO: PARSE ANONYMOUS FUNCTION "func []...." call it with defined variables
    public FunctionProcessor(Box box) {
        this.box = box;
        functionDecleration = Pattern.compile("^\\s*func\\s*\\[(.*)\\]\\s*:?\\s*(\\w+)?\\s*\\{\\s*(.*)\\s*\\}");
    }

    public boolean test (String s) {
        m = functionDecleration.matcher(s);
        return m.find();
    }

    public String convert(String name) {
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
            shouldAddFrees = true; //compiler.tokenize adds frees if body contains "return"
        }



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

    public String getCallFromFunction (String name) {
        String args = m.group(1);
        args = args.replaceAll("int", "");
        args = args.replaceAll("string", "");
        args = args.replaceAll("nstring \\*", "");
        args = args.replaceAll("\\s+", "");
        return name + "(" + args + ");\n";
    }
}
