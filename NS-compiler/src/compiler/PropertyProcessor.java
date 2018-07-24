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
        return convertAssignment(name,false);
    }

    public String convertAssignment (String name, boolean assignment){
        String obj = m.group(1);
        String prop = m.group(2);
        String args = m.group(3);

        args = "&"+obj+args;
        String line = obj + "." + prop + ":" + args;
        callProcessor.test(line);
        compiler.increaseScopeLevel();
        String ret = callProcessor.convert(line);
        if (assignment) {
            ret = "int " + name + " = " + ret;
        }
        compiler.decreaseScopeLevel();
        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        }
        return ret;
    }


}
