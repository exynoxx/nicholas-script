package compiler;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.LinkedList;

public class Compiler {

    boolean debug = false;

    PreProcessor cleaningProcessor = new PreProcessor();
    Processor[] processors = new Processor[5];

    LinkedList<String> frees;
    HashMap<Integer, LinkedList<String>> scopeHM;
    int scopeLevel = 0;

    String functionDeclerations = "";
    String statements = "";


    public Compiler() {

        processors[0] = new AssignmentProcessor(this, debug);
        processors[1] = new BranchingProcessor(this, debug);
        processors[2] = new PropertyProcessor(this,debug);
        processors[3] = new NoParseProcessor(this,debug);
        processors[4] = new CallProcessor(this, debug);

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
        String ret = string + ";\n";

        for (Processor p : processors) {
            if (p.test(string))
                return p.convert(string);
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
        c.tokenize(c.readFile("src/example.ns"));
        System.out.println("#include <stdlib.h>\n#include <stdio.h>\n\n");
        System.out.println(c.functionDeclerations);
        System.out.println("void main () {");
        System.out.println(c.statements);
        System.out.println(c.getFreeStrings());
        System.out.println("}");

    }

}
