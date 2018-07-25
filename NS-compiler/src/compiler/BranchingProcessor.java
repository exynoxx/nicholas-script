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
        branching = Pattern.compile("(if|while)\\s*(\\(.*\\))?\\s*\\{([^}]*)\\}\\s*(?:else\\s*\\{(.*)\\})?");
    }

    @Override
    public boolean test(String s) {
        m = branching.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {

        if (debug) System.out.println("-- branching");

        String type = m.group(1);
        String cond = m.group(2);
        String body = m.group(3);
        String elseBody = m.group(4);

        String line =  type + cond + "{\n";
        compiler.increaseScopeLevel();
        line += compiler.tokenize(body);
        line += compiler.getFreeStrings();
        line += "\n}";

        if (elseBody != null) {
            compiler.increaseScopeLevel();
            line += "else {\n" + compiler.tokenize(elseBody);
            line += compiler.getFreeStrings();
            line += "\n}";
        }

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }

        return line;
    }
}
