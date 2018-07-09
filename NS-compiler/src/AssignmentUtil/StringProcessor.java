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
            String line = "nstring *" + name + " = (nstring *) malloc (sizeof(nstring));\n";
            line += name + "->data = (char *) malloc (50);\n";
            line += name + "->allocsize = 50;\n";
            line += name + "->size = " + matcher.group(1).length() + ";\n";
            line += "strcpy("+name+"->data, \"" + matcher.group(1) + "\");\n";
            compiler.addFreeString("free("+name+"->data);\n");
            compiler.addFreeString("free("+name+");\n");

            compiler.insertStatement(line);
            return line;
        }
        return null;
    }
}
