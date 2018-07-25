package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StringProcessor {

    Pattern stringPattern = Pattern.compile("^\\s*\"(.*)\"");
    boolean debug;
    Compiler compiler;

    public StringProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
    }

    public String convert (String name, String s) {

        Matcher matcher = stringPattern.matcher(s);
        if (matcher.find()) {
            int size = matcher.group(1).length();

            String line = "nstring *" + name + " = (nstring *) malloc (sizeof(nstring));\n";
            line += name + "->data = (char *) malloc ("+size+");\n";
            line += name + "->size = " + size + ";\n";
            line += "strcpy("+name+"->data, \"" + matcher.group(1) + "\");\n";
            compiler.addFreeString("free("+name+"->data);\n");
            compiler.addFreeString("free("+name+");\n");

            if (compiler.getScopeLevel() == 0) {
                compiler.insertStatement(line);
            }
            return line;
        }
        return null;
    }
}
