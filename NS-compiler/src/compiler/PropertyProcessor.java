package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor {

    Pattern map;
    Pattern propertyCall;
    Matcher m;
    Box box;

    public PropertyProcessor(Box box) {
        this.box = box;
        propertyCall = Pattern.compile("^\\s*((\\d+|\\w+)\\.\\.(\\d+|\\w+)|\\w+)\\.(\\w+):(.*)");
        map = Pattern.compile("^\\s*(\\w+)\\s*(?:->|=>)\\s*(.*)");
    }

    public boolean test(String s) {
        m = propertyCall.matcher(s);
        return m.find();
    }

    public String convert(String name) {
        return convertAssignment(name, false);
    }

    public String convertAssignment(String name, boolean assignment) {
        String obj = m.group(1);
        String prop = m.group(4);
        String args = m.group(5);

        String x = checkBuildInProps(assignment);
        if (x != null) return x;

        args = "&" + obj + args;
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

    private String checkBuildInProps(boolean assignment) {
        String obj = m.group(1);
        String a = m.group(2);
        String b = m.group(3);
        String prop = m.group(4);
        String args = m.group(5).trim();
        String variable = "i";
        String body = "";

        Matcher matcher = map.matcher(args);
        if (matcher.find()) {
            variable = matcher.group(1);
            body = matcher.group(2);
            compiler.insertType(variable,Type.NUMBER);
        }
        String content = "";
        if (functionProcessor.test(body)) {
            String name = callProcessor.generateRandomName();
            String ret = functionProcessor.convert(name);
            compiler.insertFunction(ret);
            content = functionProcessor.getCallFromFunction(name);
        } else {
            compiler.increaseScopeLevel();
            content = compiler.tokenize(body + ";");
            content += compiler.getFreeStrings();

        }

        if (prop.equals("map")) {
            String line = "";
            if (a == null || b == null) {
                String newVar = callProcessor.generateRandomName();
                String arraySize;

                try {
                    arraySize = String.valueOf(compiler.getArraySize(obj));
                } catch (Exception e) {
                    arraySize = "-1";
                }

                line = "for (int "+newVar+" = 0; "+newVar+" < "+ arraySize +";"+newVar+"++) {\n";
                line += "int "+variable+" = "+obj+"["+newVar+"];\n";
                line += content;
                line += "}\n";

            } else {
                line = "for (int "+variable+" = "+a+"; "+variable+" <= "+b+"; "+variable+"++) {\n";
                line += content;
                line += "}\n";
            }
            if (compiler.getScopeLevel() == 0) {
                compiler.insertStatement(line);
            }
            return line;
        }
        return null;
    }


}
