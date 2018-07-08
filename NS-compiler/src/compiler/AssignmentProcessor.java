package compiler;

import AssignmentUtil.ArrayProcessor;
import AssignmentUtil.FunctionProcessor;
import AssignmentUtil.ObjectProcessor;

import java.util.HashMap;
import java.util.LinkedList;
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

    public AssignmentProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        ap = new ArrayProcessor(compiler, debug);
        op = new ObjectProcessor(compiler, debug);
        fp = new FunctionProcessor(compiler, debug);

        assignment = Pattern.compile("^\\s*(var)?\\s*(\\w+)\\s*=(.*)");
        call = Pattern.compile("^\\s*(\\w+):([\\w\\s\\+\\-\\/\\*]+)");


        numOrMath = Pattern.compile("^\\s*([\\s\\d\\+\\*\\/\\-]+)");
        //returnStatement = Pattern.compile("\\s*return\\s+(.*);");
        //word = Pattern.compile("^\\s*(\\w+)$");
        variableMath = Pattern.compile("^\\s*(\\w+)\\s*[\\+\\*\\/\\-\\%\\!]\\s*(\\w+)");
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
            if(compiler.getScopeLevel() == 0) {
                compiler.insertStatement(apString);
            }
            return apString;
        }

        if (debug) System.out.println("-- not ap");

        String fpString = fp.convert(name, assignee);
        if (fpString != null) {
            if(compiler.getScopeLevel() == 0) {
                compiler.insertFunction(fpString);
            }
            return fpString;
        }

        if (debug) System.out.println("-- not fp");

        String opString = op.convert(name, assignee);
        if (opString != null) {
            return opString;
        }

        if (debug) System.out.println("-- not op");

        Matcher matcher = call.matcher(assignee);
        if (matcher.find()) {
            String ret = call(name, matcher);

            compiler.insertStatement(ret);

            return ret;
        }

        if (debug) System.out.println("-- not call");
        if (debug) System.out.println("-- returning default");


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
