package AssignmentUtil;

import compiler.CallProcessor;
import compiler.Compiler;
import compiler.Type;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StringProcessor {

    CallProcessor callProcessor;
    Pattern stringPattern = Pattern.compile("^\\s*\"([^\"]*)\"\\s*$");
    Pattern stringCat = Pattern.compile("^\\s*(?:\\w+|\".*\")(?:\\s*~\\s*(?:\\w+|\".*\"))+");
    Pattern word = Pattern.compile("^\\s*\\w+");
    boolean debug;
    Compiler compiler;
    Matcher stringMatcher;
    Matcher catMatcher;

    public StringProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        callProcessor = new CallProcessor(compiler,debug,false);
    }

    public boolean testString (String s) {
        stringMatcher = stringPattern.matcher(s);
        return stringMatcher.find();
    }

    public boolean testStringCat (String s) {
        catMatcher = stringCat.matcher(s);
        return catMatcher.find();
    }

    public String convertString(String name) {
        int size = stringMatcher.group(1).length();

        String line = "nstring *" + name + " = (nstring *) malloc (sizeof(nstring));\n";
        line += name + "->data = (char *) malloc ("+size+");\n";
        line += name + "->size = " + size + ";\n";
        line += "strcpy("+name+"->data, \"" + stringMatcher.group(1) + "\");\n";
        compiler.addFreeString("free("+name+"->data);\n");
        compiler.addFreeString("free("+name+");\n");

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }
        return line;
    }

    public String convertStringCat (String name, String assignee) {

        String[] tokens = assignee.split("~");
        String size = "";
        String before = "";

        for (int i = 0; i < tokens.length; i++) {
            String tok = tokens[i].trim();

            Matcher matcher = stringPattern.matcher(tok);
            if (matcher.find()) {
                size += matcher.group(1).length() + "+";
            } else {
                if (compiler.getType(tok) == Type.NUMBER) {
                    String rname = callProcessor.generateRandomName();
                    before += "char "+rname+"[12];\n";
                    before += "snprintf("+rname+", 12, \"%d\", "+tok+");\n";
                    size += "strlen("+rname+")+";
                    tokens[i] = rname;
                } else {
                    size += tok + "->size+";
                    tokens[i] = tok + "->data";
                }
            }
        }
        size = size.substring(0,size.length()-1);

        String line = before + "nstring *" + name + " = (nstring *) malloc (sizeof(nstring));\n";
        line += name + "->size = "+size+";\n";
        line += name + "->data = (char *) malloc ("+name+"->size);\n";
        //line += "strcpy("+name+"->data, \"\");\n";
        for (String tk : tokens) {
            line += "strcat("+name+"->data, " + tk + ");\n";
        }

        compiler.addFreeString("free("+name+"->data);\n");
        compiler.addFreeString("free("+name+");\n");

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }
        return line;
    }
}
