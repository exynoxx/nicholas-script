package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AssignmentProcessor {

    Pattern assignment;
    Matcher m;
    boolean debug;
    Box box;

    public AssignmentProcessor(Box box) {
        this.box = box;
        assignment = Pattern.compile("^\\s*(var|int|double|string)?\\s*([\\w\\.]+)\\s*(\\[\\s*\\d+\\s*\\])?\\s*=\\s*(.*)");
    }

    public boolean test(String s) {
        m = assignment.matcher(s);
        return m.find();
    }


    public String detectValue(String s, boolean dynamic) {
        String name = m.group(2);
        String arrayIndex = m.group(3);
        String assignee = m.group(4).trim();

        if (ap.testNormal(assignee)) {
            String apString = ap.arrayAssignment(name,assignee,0,dynamic);
            compiler.insertType(name, Type.ARRAY);
            return apString;
        }

        if (ap.testRange(assignee)) {
            if (debug) System.out.println("---- array");
            String apString = ap.arrayAssignment(name,assignee,1,dynamic);
            compiler.insertType(name, Type.ARRAY);
            return apString;
        }

        if (ap.testEmpty(assignee)) {
            if (debug) System.out.println("---- array");
            String apString = ap.arrayAssignment(name,assignee,2,dynamic);
            compiler.insertType(name, Type.ARRAY);
            return apString;
        }

        if (fp.test(assignee)) {
            if (debug) System.out.println("---- function");
            String fpString = fp.convert(name);
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
            String ppString = pp.convertAssignment(name,true);
            if (debug) System.out.println("---- property");
            compiler.insertType(name, Type.NUMBER);
            return ppString;
        }

        String spString = checkString(name,assignee);
        if (spString != null){
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
        compiler.insertVariableValue(name,Integer.parseInt(assignee));

        return ret;
    }

    public String convert(String s) {

        String type = m.group(1);
        String name = m.group(2);
        String arrayIndex = m.group(3);
        String assignee = m.group(4).trim();
        String ret;

        if (assignee == null) {
            ret = s.trim() + ";\n";
        } else {
            assignee = assignee.trim();
            if (type != null) {
                if (type.equals("string")) {
                    String spString = checkString(name,assignee);
                    if (spString != null){
                        return spString;
                    }
                } else if (type.equals("arr")) {
                    if (ap.testNormal(assignee)) {
                        return ap.arrayAssignment(name,assignee,0,false);
                    }
                    if (ap.testRange(assignee)){
                        return ap.arrayAssignment(name,assignee,1,false);
                    }
                    if (ap.testEmpty(assignee)) {
                        return ap.arrayAssignment(name,assignee,2,false);
                    }
                }
            }
            ret = detectValue(s);
        }

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        }

        return ret;

    }

}


