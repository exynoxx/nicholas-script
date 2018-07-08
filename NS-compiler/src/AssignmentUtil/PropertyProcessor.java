package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor {

    Pattern propertyCall;
    boolean debug;
    Compiler compiler;

    public PropertyProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        propertyCall = Pattern.compile("^\\s*(\\w+\\.\\w+):(.*)");
    }

    public String convert(String name, String s) {

        String line = "";

        Matcher matcher = propertyCall.matcher(s);
        if (matcher.find()) {
            String objAndProp = matcher.group(1);
            String args = matcher.group(2).trim();

            args = args.replaceAll("\\s+", ",");
            line = "int " + name + " = " + objAndProp + "(" + args + ");\n";
            compiler.insertStatement(line);
        }
        return line;
    }


}
