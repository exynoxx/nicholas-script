package compiler;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.LinkedList;

public class Compiler {

    Box box;

    PreProcessor cleaningProcessor = new PreProcessor();
    LinkedList<String> frees;
    HashMap<Integer, LinkedList<String>> scopeHM;

    int scopeLevel = 0;

    HashMap<String, Type> typeHashMap = new HashMap<>();
    HashMap<String, Integer> arraySizeHashMap = new HashMap<>();
    HashMap<String, Integer> arrayTypeHashMap = new HashMap<>();     //type = 0:int 1:double 2:string
    HashMap<String, Integer> variableValue = new HashMap<>();

    String globalVariables = "";
    String functionDeclerations = "";
    String statements = "";


    public Compiler() {
        box = new Box();
        box.assignmentProcessor = new AssignmentProcessor(box);
        box.branchingProcessor = new BranchingProcessor(box);
        box.propertyProcessor = new PropertyProcessor(box);
        box.noParseProcessor = new NoParseProcessor(box);
        box.stringProcessor = new StringProcessor(box);
        box.arrayProcessor = new ArrayProcessor(box);
        box.callProcessor = new CallProcessor(box);
        box.stdProcessor = new StdProcessor(box);

        scopeHM = new HashMap<>();

        for (int i = 0; i < 5; i++) {
            scopeHM.put(i, new LinkedList<>());
        }

        frees = scopeHM.get(0);
        functionDeclerations += "typedef struct _nstring {\nchar *data;\nint size;\n} nstring;\n\n";
        functionDeclerations += "void prints(nstring * x) {\nprintf(\"%s\", x->data);\n}\n void printi(int x) {\nprintf(\"%d\", x);\n}\n";
        functionDeclerations += "void printls(nstring * x) {\nprintf(\"%s\\n\", x->data);\n}\n void printli(int x) {\nprintf(\"%d\\n\", x);\n}\n";
    }

    String readFile(Striout1ng path) throws IOException {
        byte[] encoded = Files.readAllBytes(Paths.get(path));
        return new String(encoded);
    }

    public LinkedList<String> tokenize(String string) {
        String out = "";

        string = cleaningProcessor.clean(string);
        String globalCode = cleaningProcessor.extractGlobalCode(string);
        if (globalCode != null) {
            increaseScopeLevel();
            globalVariables += tokenize(globalCode);
            decreaseScopeLevel();
            string = cleaningProcessor.extractGlobalCodeAndReturnRest(string);
        }

        LinkedList<String> tokens = cleaningProcessor.partition(string);
        LinkedList<String> output = new LinkedList<>();
        for (String s : tokens) {
            String processed = processString(s);
            output.add(processed);
            out += processed;
        }

        if (scopeLevel == 0) {
            insertStatement(out);
        }

        return tokens;
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

    public void insertArrayType (String name, int value) {
        arrayTypeHashMap.put(name,value);
    }

    public int getArrayType (String name) {
        return arrayTypeHashMap.get(name);
    }

    public static void main(String[] args) throws IOException {

        Compiler c = new Compiler();






        String name = "src/examples/tmp.ns";
        if (args.length > 0) {
            name = args[0];
        }

        c.tokenize(c.readFile(name));
        System.out.println("#include <stdlib.h>\n#include <stdio.h>\n#include <string.h>\n\n");
        System.out.println(c.globalVariables);
        System.out.println(c.functionDeclerations);
        System.out.println("void main () {");
        System.out.println(c.statements);
        System.out.println(c.getFreeStrings());
        System.out.println("}");

    }

}
