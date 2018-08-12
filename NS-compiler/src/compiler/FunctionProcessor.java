package compiler;

import compiler.Compiler;

import java.util.LinkedList;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class FunctionProcessor {

    Pattern functionDecleration = Pattern.compile("^\\s*func\\s*\\[(.*)\\]\\s*:?\\s*(\\w+)?\\s*\\{\\s*(.*)\\s*\\}");
    Matcher m;
    Box box;

    //TODO: PARSE ANONYMOUS FUNCTION "func []...." call it with defined variables
    public FunctionProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = functionDecleration.matcher(s);
        return m.find();
    }

    public String convert(String name) {
        return functionDecleration(name); //variableValueType called inside function
    }

    private String functionDecleration(String name) {

        String args = m.group(1);
        String returnType = m.group(2);
        if (returnType == null) returnType = "void";
        String body = m.group(3);
        String returnValue = "";

        //TODO: handle return and frees

        //convert argument types to c. strings and arrays will always have type char* and void**
        args = args.replaceAll("string\\s+(\\w+)", "char *$1");
        args = args.replaceAll("arr\\s+(\\w+)", "void **$1");

        //translate body
        String translatedBody = "";
        box.compiler.increaseScopeLevel();
        LinkedList<String> input = box.compiler.tokenize(body);
        boolean hasFreed = false;
        for (String s : input) {

            //if body contains free statement. add free-statements before
            if (s.matches("^\\s+return")) {
                translatedBody += box.compiler.getFreeStrings();
                translatedBody += s;
                hasFreed = true;
            } else {
                translatedBody += s;
            }
        }
        if (!hasFreed) {
            translatedBody += box.compiler.getFreeStrings();
        }

        //anonymous function. give it random name and call it with same arguments as decleration
        if (name == null) {
            name = box.compiler.generateRandomName();
            String newArg = args.replaceAll("\\w+\\s+(\\w+)","$1");
            returnValue = name + "(" + newArg + ");\n";
        }

        String declerarion = returnType + " " + name + "(" + args + ") {\n";
        declerarion += translatedBody;
        declerarion += "}\n\n";
        box.compiler.insertFunction(declerarion);

        return returnValue;
    }
}