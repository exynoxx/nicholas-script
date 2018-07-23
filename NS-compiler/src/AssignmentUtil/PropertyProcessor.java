package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor {

    Pattern propertyCall;
    boolean debug;
    Compiler compiler;
    Matcher m;

    public PropertyProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        propertyCall = Pattern.compile("^\\s*(\\w+\\.\\w+):(.*)");
    }

    public boolean test (String s) {
        m = propertyCall.matcher(s);
        return m.find();
    }

    public String convert(String name) {

        String line = null;
        String objAndProp = m.group(1);
        String args = m.group(2).trim();

        args = args.replaceAll("\\s+", ",");
        line = "int " + name + " = " + objAndProp + "(" + args + ");\n";
        compiler.insertStatement(line);
        return line;
    }


}
