package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class BranchingProcessor {

    Pattern branching = Pattern.compile("^\\s*(if|while)\\s*(.*)");

    Matcher m;
    Box box;

    public BranchingProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = branching.matcher(s);
        return m.find();
    }

    public String convert(String s) {

        //condition
        int startCond = s.indexOf('(');
        int endCond = getIndexOf(s, "()");
        String condition = s.substring(startCond + 1, endCond);

        //if/while body
        String rest = s.substring(endCond + 1, s.length());
        int startBody = rest.indexOf('{');
        int endBody = getIndexOf(rest, "{}");
        String body = rest.substring(startBody + 1, endBody);

        //else body
        String elseBody = null;
        rest = rest.substring(endBody + 1, rest.length());
        if (rest.contains("else")) {
            int startElse = rest.indexOf('{');
            int endElse = getIndexOf(rest, "{}");
            elseBody = rest.substring(startElse + 1, endElse);
        }

        String type = m.group(1);
        String line = type + " (" + condition + ") {\n";
        box.compiler.increaseScopeLevel();
        for (String tok : box.compiler.tokenize(body.trim())){
            line += tok;
        }
        line += box.compiler.getFreeStrings();
        line += "}\n";

        if (elseBody != null) {
            line += "else {\n";
            box.compiler.increaseScopeLevel();
            for (String tok : box.compiler.tokenize(elseBody.trim())){
                line += tok;
            }
            line += box.compiler.getFreeStrings();
            line += "}\n";
        }
        return line;
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
}
