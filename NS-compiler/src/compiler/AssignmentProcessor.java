package compiler;

import AssignmentUtil.*;

import java.util.HashMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AssignmentProcessor implements Processor {

    Pattern assignment;

    Pattern call;
    Pattern numOrMath;
    Pattern variableMath;

    Matcher m;
    boolean debug;
    Compiler compiler;

    HashMap<String, String> hashMapReturnType;

    ArrayProcessor ap;
    ObjectProcessor op;
    FunctionProcessor fp;
    PropertyProcessor pp;
    StringProcessor sp;

    public AssignmentProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        ap = new ArrayProcessor(compiler, debug);
        op = new ObjectProcessor(compiler, debug);
        sp = new StringProcessor(compiler, debug);
        fp = new FunctionProcessor(compiler, debug);
        pp = new PropertyProcessor(compiler, debug);

        assignment = Pattern.compile("^\\s*(var)?\\s*([\\w\\.]+)\\s*=(.*)");
        call = Pattern.compile("^\\s*(\\w+):(.*)");


        //numOrMath = Pattern.compile("^\\s*([\\s\\d\\+\\*\\/\\-]+)");
        //variableMath = Pattern.compile("^\\s*(\\w+)\\s*[\\+\\*\\/\\-\\%\\!]\\s*(\\w+)");
        hashMapReturnType = new HashMap<>();
    }

    @Override
    public boolean test(String s) {
        m = assignment.matcher(s);
        return m.find();
    }

    public String cc(String s) {
        //boolean dynamic = false;

        if (debug) System.out.println("-- assignment");
        //if(m.group(1) == null) dynamic = true;
        String name = m.group(2);
        String assignee = m.group(3).trim();

        String apString = ap.convert(name, assignee);
        if (apString != null) {
            if (debug) System.out.println("---- array");
            compiler.insertType(name, Type.ARRAY);
            if(compiler.getScopeLevel() == 0) {
                compiler.insertStatement(apString);
            }
            return apString;
        }


        String fpString = fp.convert(name, assignee);
        if (fpString != null) {
            if (debug) System.out.println("---- function");
            compiler.insertType(name,Type.FUNCTION);
            compiler.insertFunction(fpString);
            return fpString;
        }


        String opString = op.convert(name, assignee);
        if (opString != null) {
            compiler.insertType(name,Type.OBJECT);
            return opString;
        }

        String ppString = pp.convert(name, assignee);
        if (ppString != null) {
            if (debug) System.out.println("---- property");
            compiler.insertType(name,Type.NUMBER);
            return ppString;
        }

        String spString = sp.convert(name,assignee);
        if (spString != null) {
            if (debug) System.out.println("---- string");
            compiler.insertType(name,Type.STRING);
            return spString;
        }

        Matcher matcher = call.matcher(assignee);
        if (matcher.find()) {
            if (debug) System.out.println("---- function call");
            String ret = call(name, matcher);
            compiler.insertStatement(ret);
            compiler.insertType(name,Type.NUMBER);

            return ret;
        }

        if (debug) System.out.println("-- returning default");

        compiler.insertType(name,Type.NUMBER);

        String pre = (m.group(1) == null) ? "" : "int ";
        String ret = pre + name + " = " + assignee + ";\n";

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        }

        return ret;
    }

    @Override
    public String convert(String s) {

        String ret = cc(s);
        return ret;

    }


    private String call(String name, Matcher matcher) {
        String args = matcher.group(2).trim();
        String pre = (m.group(1) == null) ? "" : "int ";
        args = args.replaceAll("\\s+", ",");
        String line = pre + name + " = " + matcher.group(1) + "(" + args + ");\n";
        return line;
    }
}
