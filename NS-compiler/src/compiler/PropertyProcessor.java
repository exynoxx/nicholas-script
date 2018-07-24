package compiler;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor implements Processor {

    Pattern propertyCall;
    boolean debug;
    Compiler compiler;
    CallProcessor callProcessor;
    Matcher m;

    public PropertyProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        callProcessor = new CallProcessor(compiler,debug,true);
        propertyCall = Pattern.compile("^\\s*(\\w+)\\.(\\w+):(.*)");
    }

    public boolean test (String s) {
        m = propertyCall.matcher(s);
        return m.find();
    }

    public String convert(String name) {

        String obj = m.group(1);
        String prop = m.group(2);
        String args = m.group(3);

        args = "&"+obj+args;
        String line = obj + "." + prop + ":" + args;
        callProcessor.test(line);
        return callProcessor.convert(line);

        /*
        args = args.replaceAll("\\s+", ",");
        line = "int " + name + " = " + objAndProp + "(" + args + ");\n";
        compiler.insertStatement(line);
        return line;
        */
    }


}
