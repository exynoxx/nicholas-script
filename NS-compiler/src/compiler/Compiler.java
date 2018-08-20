package compiler;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.LinkedList;
import java.util.Map;
import java.util.Random;

public class Compiler {

    Box box;

    PreProcessor cleaningProcessor = new PreProcessor();
    HashMap<Integer, HashMap<String,String>> scopeHM;

    int scopeLevel = 0;

    HashMap<String, Type> typeHashMap = new HashMap<>();
    HashMap<String, Type> arrayTypeHashMap = new HashMap<>();
    HashMap<String, Integer> arraySizeHashMap = new HashMap<>();
    HashMap<String, Integer> variableValueHashMap = new HashMap<>();
    HashMap<String, String> frees = new HashMap<>();

    String globalVariables = "";
    String functionDeclerations = "";
    String forwardDeclerations = "";
    String statements = "";

    Random random;

    public Compiler() {
        box = new Box();
        box.assignmentProcessor = new AssignmentProcessor(box);
        box.branchingProcessor = new BranchingProcessor(box);
        box.functionProcessor = new FunctionProcessor(box);
        box.propertyProcessor = new PropertyProcessor(box);
        box.noParseProcessor = new NoParseProcessor(box);
        box.stringProcessor = new StringProcessor(box);
        box.arrayProcessor = new ArrayProcessor(box);
        box.callProcessor = new CallProcessor(box);
        box.stdProcessor = new StdProcessor(box);
        box.compiler = this;
        random = new Random();

        scopeHM = new HashMap<>();

        for (int i = 0; i < 5; i++) {
            scopeHM.put(i, new HashMap<>());
        }

        frees = scopeHM.get(0);
        functionDeclerations += "void prints(char * x) {\nprintf(\"%s\", x);\n}\n void printi(int x) {\nprintf(\"%d\", x);\n}\n";
        functionDeclerations += "void printls(char * x) {\nprintf(\"%s\\n\", x);\n}\n void printli(int x) {\nprintf(\"%d\\n\", x);\n}\n";
    }

    String readFile(String path) throws IOException {
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

        LinkedList<String> input = cleaningProcessor.partition(string);
        LinkedList<String> output = new LinkedList<>();
        for (String s : input) {
            String processed = processString(s);
            output.add(processed);
            out += processed;
        }

        if (scopeLevel == 0) {
            insertStatement(out);
        }

        return output;
    }

    public String processString(String string) {
        String ret = string.trim() + ";\n";

        if (box.assignmentProcessor.test(string)) ret = box.assignmentProcessor.convert();

        else if (box.stringProcessor.testString(string)) ret = box.stringProcessor.convertString(null, null);
        else if (box.stringProcessor.testEmpty(string)) ret = box.stringProcessor.convertEmpty(string);
        else if (box.stringProcessor.testStringCat(string)) ret = box.stringProcessor.convertStringCat(string, false);

        else if (box.propertyProcessor.test(string)) ret = box.propertyProcessor.convert();
        else if (box.branchingProcessor.test(string)) ret = box.branchingProcessor.convert(string);
        else if (box.callProcessor.test(string)) ret = box.callProcessor.convert(null,false);
        else if (box.stdProcessor.test(string)) ret = box.stdProcessor.convert(string);
        else if (box.noParseProcessor.test(string)) ret = box.noParseProcessor.convert(string);
        else if (box.functionProcessor.test(string)) ret = box.functionProcessor.convert(null);

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

        for (Map.Entry<String, String> entry : frees.entrySet()) {
            ret += entry.getValue();
        }
        frees.clear();

        scopeLevel--;
        if (scopeLevel < 0) scopeLevel = 0;
        frees = scopeHM.get(scopeLevel);
        return ret;
    }

    public void insertForwardDecleration (String s) {
        forwardDeclerations += s;
    }

    public String removeOneFreeString(String name) {
        return frees.remove(name);
    }

    public String generateRandomName() {

        int leftLimit = 97; // letter 'a'
        int rightLimit = 122; // letter 'z'
        int targetStringLength = 5;

        StringBuilder buffer = new StringBuilder(targetStringLength);
        for (int i = 0; i < targetStringLength; i++) {
            int randomLimitedInt = leftLimit + (int) (random.nextFloat() * (rightLimit - leftLimit + 1));
            buffer.append((char) randomLimitedInt);
        }

        //String generatedString = buffer.toString();
        return buffer.toString();
    }

    public void addFreeString(String name, String s) {

        if (name == null) {
            name = "default";
            frees.put(name,frees.get(name) + s);
        }
        frees.put(name, s);
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

    public void insertArraySize(String name, int size) {
        arraySizeHashMap.put(name, size);
    }

    public int getArraySize(String name) {
        return arraySizeHashMap.get(name);
    }

    public int getVariableValue(String name) {
        return variableValueHashMap.get(name);
    }

    public void insertVariableValue(String name, int value) {
        variableValueHashMap.put(name, value);
    }

    public void insertArrayType(String name, Type value) {
        arrayTypeHashMap.put(name, value);
    }

    public Type getArrayType(String name) {
        return arrayTypeHashMap.get(name);
    }

    public static void main(String[] args) throws IOException {

        Compiler c = new Compiler();

        String name = "src/examples/types.ns";
        if (args.length > 0) {
            name = args[0];
        }

        c.tokenize(c.readFile(name));
        System.out.println("#include <stdlib.h>\n#include <stdio.h>\n#include <string.h>\n\n");
        System.out.println(c.globalVariables);
        System.out.println(c.forwardDeclerations);
        System.out.println(c.functionDeclerations);
        System.out.println("void main () {");
        System.out.println(c.statements);
        System.out.println(c.getFreeStrings());
        System.out.println("}");

    }

}
