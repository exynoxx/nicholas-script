package AssignmentUtil;

import compiler.Compiler;
import compiler.PreProcessor;

import java.util.LinkedList;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ObjectProcessor {

    private final Pattern variableDecleration;
    private final Pattern functionDecleration;
    private final Pattern objectDecleration;
    boolean debug;
    Compiler compiler;
    PreProcessor preProcessor;
    Matcher m;

    public ObjectProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        this.preProcessor = new PreProcessor();
        variableDecleration = Pattern.compile("^\\s*(int|string)\\s*(\\w+)");
        functionDecleration = Pattern.compile("^\\s*(int|string)\\s*(\\w+)\\s*=\\s*func\\s*\\[(.*)\\]\\s*\\{\\s*(.*)\\s*\\}");
        objectDecleration = Pattern.compile("^\\s*object\\s*\\{\\s*(.*)\\s*\\}");
    }

    public boolean test (String s) {
        m = objectDecleration.matcher(s);
        return m.find();
    }

    public String convert(String name, String s) {
        if (debug) System.out.println("---- object");
        return object(name);
    }


    private String object(String name) {
        String body = m.group(1);

        LinkedList<String> tokens = preProcessor.partition(body);

        String headerVariables = "";
        String headerFunctions = "";
        String functionDefinitions = "";
        String statements = "";


        /* ### DEFINITIONS ### */
        Matcher localMatcher;
        for (String token : tokens) {
            localMatcher = functionDecleration.matcher(token);
            if (localMatcher.find()) {
                headerFunctions += localMatcher.group(1) + "(*" + localMatcher.group(2) + ")(" + localMatcher.group(3) + ");\n";

                functionDefinitions += localMatcher.group(1) + " " + localMatcher.group(2) + "0" + " (" + localMatcher.group(3) + ") {\n";
                compiler.increaseScopeLevel();
                functionDefinitions += compiler.tokenize(localMatcher.group(4).trim());
                //functionDefinitions += compiler.getFreeStrings();
                functionDefinitions += "\n}";
                statements += name + "." + localMatcher.group(2) + " = " + localMatcher.group(2) + "0;\n";
                continue;
            }
            localMatcher = variableDecleration.matcher(token);
            if (localMatcher.find()) {
                headerVariables += localMatcher.group(1) + " " + localMatcher.group(2) + ";\n";
            }
        }
        /* ########################3 */
        String struct = "typedef struct __" + name + " {\n";
        struct += headerVariables;
        struct += headerFunctions;
        struct += "} _" + name + ";\n";

        compiler.insertFunction(struct);
        compiler.insertFunction(functionDefinitions);
        compiler.insertStatement("_" + name + " " + name + ";\n");
        compiler.insertStatement(statements);

        return struct;
    }
}
