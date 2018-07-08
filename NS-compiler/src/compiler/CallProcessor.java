package compiler;

import compiler.Processor;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CallProcessor implements Processor {

    boolean debug;
    Pattern functionCall;
    Matcher m;
    Compiler compiler;


    public CallProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        functionCall = Pattern.compile("^\\s*(\\w+):(.*)");
    }

    @Override
    public boolean test(String s) {
        m = functionCall.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {
        String name = m.group(1);
        String args = m.group(2).trim();

        args = args.replaceAll("\\s+", ",");
        String line = name + "(" + args + ");\n";
        compiler.insertStatement(line);
        return line;
    }

}
