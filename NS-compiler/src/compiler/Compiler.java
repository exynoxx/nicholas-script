package compiler;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.LinkedList;

public class Compiler {

    boolean debug = false;

    PreProcessor cleaningProcessor = new PreProcessor();
    Processor[] processors = new Processor[6];

    LinkedList<String> frees;
    HashMap<Integer, LinkedList<String>> scopeHM;

    int scopeLevel = 0;

    HashMap<String, Type> typeHashMap = new HashMap<>();
    HashMap<String, Integer> arraySizeHashMap = new HashMap<>();
    HashMap<String, Integer> variableValue = new HashMap<>();

    String functionDeclerations = "";
    String statements = "";


    public Compiler() {

        processors[0] = new AssignmentProcessor(this, debug);
        processors[1] = new BranchingProcessor(this, debug);
        processors[2] = new NoParseProcessor(this, debug);
        processors[3] = new PropertyProcessor(this, debug);
        processors[4] = new StdProcessor(this, debug);
        processors[5] = new CallProcessor(this, debug, true);

        scopeHM = new HashMap<>();

        for (int i = 0; i < 5; i++) {
            scopeHM.put(i, new LinkedList<>());
        }

        frees = scopeHM.get(0);
        functionDeclerations += "typedef struct _nstring {\nchar *data;\nint size;\n} nstring;\n\n";
        //functionDeclerations += "var prints = func [string x] {(c) {printf(\"%s\\n\", x->data);};};var printi = func [int x] {(c) {printf(\"%d\\n\", x);};};";
        functionDeclerations += "void prints(nstring * x) {\nprintf(\"%s\\n\", x->data);\n}\n void printi(int x) {\nprintf(\"%d\\n\", x);\n}\n";
    }

    String readFile(String path) throws IOException {
        byte[] encoded = Files.readAllBytes(Paths.get(path));
        return new String(encoded);
    }

    public String tokenize(String string) {
        String out = "";

        string = cleaningProcessor.clean(string);
        LinkedList<String> tokens = cleaningProcessor.partition(string);

        for (String s : tokens) {
            if (debug) System.out.println("input: " + s);
            //all "after"-statements come before return-statement

            if (s.contains("return")) {
                out += getFreeStrings();
            }

            out += processString(s);
        }
        //out += getFreeStrings();


        return out;
    }

    public String processString(String string) {
        String ret = string + ";\n";

        for (Processor p : processors) {
            if (p.test(string))
                return p.convert(string);
        }
        return ret;
    }

    public void increaseScopeLevel() {
        scopeLevel++;
        frees = scopeHM.get(scopeLevel);
    }

    public void decreaseScopeLevel() {
        scopeLevel--;
        frees = scopeHM.get(scopeLevel);
    }

    public String getFreeStrings() {
        String ret = "";
        while (frees.size() > 0) {
            ret += frees.removeFirst();
        }
        scopeLevel--;
        if (scopeLevel < 0) scopeLevel = 0;
        frees = scopeHM.get(scopeLevel);
        return ret;
    }

    public void addFreeString(String s) {
        frees.add(s);
    }

    public void insertFunction(String s) {
        functionDeclerations += s;
    }

    public int getScopeLevel() {
        return scopeLevel;
    }

    public void insertStatement(String s) {
        statements += s;
    }

    public void insertType(String name, Type type) {
        typeHashMap.put(name, type);
    }

    public Type getType(String name) {
        return typeHashMap.get(name);
    }

    public void insertArraySize (String name, int size) {
        arraySizeHashMap.put(name,size);
    }

    public int getArraySize (String name) {
        return arraySizeHashMap.get(name);
    }

    public int getVariableValue(String name) {
        return variableValue.get(name);
    }

    public void insertVariableValue(String name, int value) {
        variableValue.put(name,value);
    }

    public static void main(String[] args) throws IOException {

        String name = "src/examples/strings.ns";
        if (args.length > 0) {
            name = args[0];
        }

        Compiler c = new Compiler();
        c.tokenize(c.readFile(name));
        System.out.println("#include <stdlib.h>\n#include <stdio.h>\n#include <string.h>\n\n");
        System.out.println(c.functionDeclerations);
        System.out.println("void main () {");
        System.out.println(c.statements);
        System.out.println(c.getFreeStrings());
        System.out.println("}");

    }

}
