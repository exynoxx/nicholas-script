package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AssignmentProcessor {

    Pattern assignment = Pattern.compile("^\\s*(var|int|double|string)?\\s*([\\w\\.]+)\\s*(\\[\\s*\\d+\\s*\\])?\\s*=\\s*(.*)");
    Matcher m;
    boolean debug;
    Box box;

    public AssignmentProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = assignment.matcher(s);
        return m.find();
    }

    public String convert() {

        String type = m.group(1);
        String name = m.group(2);
        String arrayIndex = m.group(3);
        String assignee = m.group(4).trim();
        boolean dynamic = (type == null);

        if (arrayIndex != null) {

        } else {
            //***STRINGS
            if (box.stringProcessor.testString(assignee)) {
                box.compiler.insertType(name, Type.STRING);
                return box.stringProcessor.convertString(name, null);
            }
            if (box.stringProcessor.testEmpty(assignee)) {
                box.compiler.insertType(name, Type.STRING);
                return box.stringProcessor.convertEmpty(name);
            }
            if (box.stringProcessor.testStringCat(assignee)) {
                box.compiler.insertType(name, Type.STRING);
                return box.stringProcessor.convertStringCat(name, null);
            }

            //***ARRAYS
            if (box.arrayProcessor.testNormal(assignee)){
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayNormal(name,dynamic);
            }
            if (box.arrayProcessor.testEmpty(assignee)){
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayEmpty(name,dynamic);
            }
            if (box.arrayProcessor.testRange(assignee)){
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayRange(name,dynamic);
            }

            //***OTHER
            if (box.callProcessor.test(assignee)) {
                box.compiler.insertType(name, box.compiler.getType(name));
                return box.callProcessor.convert(false);
            }
            if (box.functionProcessor.test(assignee)) {
                //will register type inside
                return box.functionProcessor.convert(name);
            }
        }

        return matchSimpleTypes(name, assignee);
    }

    String matchSimpleTypes (String name, String s) {
        if (s.matches("\\d+\\.\\d+")) {
            return "double " + name + " = " + s + ";\n";
        } else {
            return "int " + name + " = " + s + ";\n";
        }
    }



}


