package compiler;

import javax.script.ScriptEngine;
import javax.script.ScriptEngineManager;
import javax.script.ScriptException;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AssignmentProcessor {

    Pattern assignment = Pattern.compile("^\\s*(var|int|double|string|arr)?\\s*([\\w\\.]+)\\s*(\\[\\s*\\d+\\s*\\])?\\s*=\\s*(.*)");
    Matcher m;
    boolean debug;
    Box box;

    ScriptEngine engine;

    public AssignmentProcessor(Box box) {
        this.box = box;
        ScriptEngineManager mgr = new ScriptEngineManager();
        engine = mgr.getEngineByName("JavaScript");
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
            return box.arrayProcessor.convertArrayWrite(name,arrayIndex,assignee);
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
                return box.stringProcessor.convertStringCat(name, dynamic);
            }

            //***ARRAYS
            if (box.arrayProcessor.testNormal(assignee)) {
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayNormal(name, dynamic);
            }
            if (box.arrayProcessor.testEmpty(assignee)) {
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayEmpty(name, dynamic);
            }
            if (box.arrayProcessor.testRange(assignee)) {
                box.compiler.insertType(name, Type.ARRAY);
                return box.arrayProcessor.convertArrayRange(name, dynamic);
            }
            if (box.arrayProcessor.testArrayRead(assignee)) {
                //will register type inside
                return box.arrayProcessor.convertArrayRead(name, assignee);
            }

            //***OTHER
            if (box.callProcessor.test(assignee)) {
                box.compiler.insertType(name, box.compiler.getType(name));
                return box.callProcessor.convert(name,false);
            }
            if (box.functionProcessor.test(assignee)) {
                //will register type inside
                return box.functionProcessor.convert(name);
            }
        }

        return matchSimpleTypes(name, assignee, dynamic);
    }

    String matchSimpleTypes(String name, String s, boolean dynamic) {

        Pattern word = Pattern.compile("[a-zA-Z]+");
        Matcher tmpMatcher = word.matcher(s);
        String pre = "";

        while (tmpMatcher.find()) {

            //value dosent exist
            String variable = null;
            try {
                variable = tmpMatcher.group(0);
                Integer value = box.compiler.getVariableValue(variable);
            } catch (Exception e) {
                pre += variable + " = 0;";
                box.compiler.insertVariableValue(variable,0);
                continue;
            }

        }

        String jsOut = null;
        try {
            jsOut = engine.eval(pre + s).toString();
        } catch (ScriptException e) {
        }

        Double val = Double.valueOf(jsOut);
        box.compiler.insertVariableValue(name, (int) Math.floor(val));

        if (val % 1 == 0) {
            String ret = name + " = " + s + ";\n";
            if (!dynamic) ret = "int " + ret;
            return ret;
        } else {
            String ret = name + " = " + s + ";\n";
            if (!dynamic) ret = "double " + ret;
            return ret;
        }
    }


}


