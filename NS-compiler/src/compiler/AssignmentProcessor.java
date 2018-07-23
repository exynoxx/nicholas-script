package compiler;

import AssignmentUtil.*;

import java.util.HashMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AssignmentProcessor implements Processor {

    Pattern assignment;

    Matcher m;
    boolean debug;
    Compiler compiler;

    HashMap<String, String> hashMapReturnType;

    ArrayProcessor ap;
    ObjectProcessor op;
    FunctionProcessor fp;
    PropertyProcessor pp;
    StringProcessor sp;
    CallProcessor cp;

    public AssignmentProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        ap = new ArrayProcessor(compiler, debug);
        op = new ObjectProcessor(compiler, debug);
        sp = new StringProcessor(compiler, debug);
        fp = new FunctionProcessor(compiler, debug);
        pp = new PropertyProcessor(compiler, debug);
        cp = new CallProcessor(compiler, debug, false);

        assignment = Pattern.compile("^\\s*(var|int|string|arr)?\\s*([\\w\\.]+)\\s*=(.*)");

        hashMapReturnType = new HashMap<>();
    }

    @Override
    public boolean test(String s) {
        m = assignment.matcher(s);
        return m.find();
    }

    public String detectValue(String s) {
        //boolean dynamic = false;

        if (debug) System.out.println("-- assignment");
        //if(m.group(1) == null) dynamic = true;
        String name = m.group(2);
        String assignee = m.group(3).trim();

        if (ap.testNormal(assignee)) {
            if (debug) System.out.println("---- array");
            String apString = ap.convert(name,assignee,false);
            compiler.insertType(name, Type.ARRAY);
            if (compiler.getScopeLevel() == 0) {
                compiler.insertStatement(apString);
            }
            return apString;
        }

        if (ap.testRange(assignee)) {
            if (debug) System.out.println("---- array");
            String apString = ap.convert(name,assignee,true);
            compiler.insertType(name, Type.ARRAY);
            if (compiler.getScopeLevel() == 0) {
                compiler.insertStatement(apString);
            }
            return apString;
        }

        if (fp.test(assignee)) {
            if (debug) System.out.println("---- function");
            String fpString = fp.convert(name, assignee);
            compiler.insertType(name, Type.FUNCTION);
            compiler.insertFunction(fpString);
            return fpString;
        }


        if (op.test(assignee)) {
            String opString = op.convert(name, assignee);
            compiler.insertType(name, Type.OBJECT);
            return opString;
        }

        if (pp.test(assignee)) {
            String ppString = pp.convert(name);
            if (debug) System.out.println("---- property");
            compiler.insertType(name, Type.NUMBER);
            return ppString;
        }

        String spString = sp.convert(name, assignee);
        if (spString != null) {
            if (debug) System.out.println("---- string");
            compiler.insertType(name, Type.STRING);
            return spString;
        }

        if (cp.test(assignee)) {
            if (debug) System.out.println("---- function call");
            String ret = name + " = " + cp.convert(assignee);
            compiler.insertStatement(ret);
            compiler.insertType(name, Type.NUMBER);

            return ret;
        }

        if (debug) System.out.println("-- returning default");

        compiler.insertType(name, Type.NUMBER);

        String pre = (m.group(1) == null) ? "" : "int ";
        String ret = pre + name + " = " + assignee + ";\n";

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        }

        return ret;
    }

    @Override
    public String convert(String s) {

        String type = m.group(1);
        String name = m.group(2);
        String assignee = m.group(3).trim();

        if (type.equals("string")) {
            return sp.convert(name, assignee);
        } else if (type.equals("arr")) {
            if (ap.testNormal(assignee)) {
                return ap.convert(name,assignee,false);
            } else {
                ap.testRange(assignee);
                return ap.convert(name,assignee,true);
            }
        }

        String ret = detectValue(s);
        return ret;

    }

}
