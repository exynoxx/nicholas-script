package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class BranchingProcessor implements Processor {

    Pattern branching;

    Matcher m;
    boolean debug;
    Compiler compiler;

    public BranchingProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        branching = Pattern.compile("^\\s*(if|while)\\s*(.*)");
    }

    public int getIndexOf(String s, String delim) {
        char[] array = s.toCharArray();
        int scopeCount = 0;

        for (int i = 0; i < array.length; i++) {
            if (array[i] == delim.charAt(0)) scopeCount++;
            if (array[i] == delim.charAt(1)) scopeCount--;
            if (scopeCount == 0 && array[i] == delim.charAt(1)) return i;
        }
        return -1;
    }

    @Override
    public boolean test(String s) {
        m = branching.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {

        if (debug) System.out.println("-- branching");

        //condition
        int startCond = s.indexOf('(');
        int endCond = getIndexOf(s, "()");
        String condition = s.substring(startCond + 1, endCond);

        //if/while body
        String rest = s.substring(endCond + 1, s.length());
        int startBody = rest.indexOf('{');
        int endBody = getIndexOf(rest, "{}");
        String body = rest.substring(startBody+1, endBody);

        //else body
        String elseBody = null;
        rest = rest.substring(endBody+1,rest.length());
        if (rest.contains("else")) {
            int startElse = rest.indexOf('{');
            int endElse = getIndexOf(rest,"{}");
            elseBody = rest.substring(startElse+1,endElse);
        }

        String line = m.group(1) + " ("+condition+") {\n";
        compiler.increaseScopeLevel();
        line += compiler.tokenize(body.trim());
        line += compiler.getFreeStrings();
        line += "\n}";

        if (elseBody != null) {
            compiler.increaseScopeLevel();
            line += "else {\n";
            line += compiler.tokenize(elseBody.trim());
            line += compiler.getFreeStrings();
            line += "\n}\n";
        }

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }

        return line;
    }
}
