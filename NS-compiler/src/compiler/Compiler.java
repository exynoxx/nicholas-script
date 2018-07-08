package compiler;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedList;

public class Compiler {

    boolean debug = false;

    PreProcessor cleaningProcessor = new PreProcessor();

    Processor assignmentProcessor = new AssignmentProcessor(this, debug);
    Processor branchingProcessor = new BranchingProcessor(this, debug);
    Processor propertyProcessor = new PropertyProcessor(this,debug);
    Processor callProcessor = new CallProcessor(debug);

    LinkedList<String> frees;
    HashMap<Integer, LinkedList<String>> scopeHM;
    int scopeLevel = 0;

    String functionDeclerations = "";
    String statements = "";


    public Compiler() {
        scopeHM = new HashMap<>();

        for (int i = 0; i < 5; i++) {
            scopeHM.put(i, new LinkedList<>());
        }

        frees = scopeHM.get(0);
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
            //all "free"-statements come before return-statement
            if (s.contains("return")) {
                out += getFreeStrings();
            }

            out += processString(s);
        }


        return out;
    }

    public String processString(String string) {
        String ret;
        if (assignmentProcessor.test(string)) {
            ret = assignmentProcessor.convert(string);
        } else if (branchingProcessor.test(string)){
            ret = branchingProcessor.convert(string);
        } else if (callProcessor.test(string)) {
            ret = callProcessor.convert(string);
        } else {
            return string + ";\n";
        }

        return ret;
    }

    public void increaseScopeLevel () {
        scopeLevel++;
        frees = scopeHM.get(scopeLevel);
    }

    public String getFreeStrings () {
        String ret = "";
        for (String s : frees) {
            ret += s;
            frees.remove(s);
        }
        scopeLevel--;
        if (scopeLevel < 0) scopeLevel = 0;
        frees = scopeHM.get(scopeLevel);
        return ret;
    }

    public void addFreeString (String s) {
        frees.add(s);
    }

    public void insertFunction (String s) {
        functionDeclerations += s;
    }

    public int getScopeLevel() {
        return scopeLevel;
    }

    public void insertStatement (String s) {
        statements += s;

    }

    public static void main(String[] args) throws IOException {

        Compiler c = new Compiler();
        c.tokenize(c.readFile("/home/nicholas/git/D-experiments/NS-compiler/src/example.ns"));
        System.out.println("#include <stdlib.h>\n#include <stdio.h>\n\n");
        System.out.println(c.functionDeclerations);
        System.out.println("void main () {");
        System.out.println(c.statements);
        System.out.println(c.getFreeStrings());
        System.out.println("}");

    }

}
