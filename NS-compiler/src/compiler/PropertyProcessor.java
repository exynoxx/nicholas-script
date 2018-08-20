package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor {

    //array map
    //
    Pattern map = Pattern.compile("^\\s*(\\w+)\\s*(?:->|=>)\\s*(.*)");
    Pattern propertyCall = Pattern.compile("^\\s*((\\d+|\\w+)\\.\\.(\\d+|\\w+)|\\w+|\\[.*\\])\\.(\\w+):(.*)");
    Matcher m;
    Matcher mapMatcher;
    Box box;

    public PropertyProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = propertyCall.matcher(s);
        return m.find();
    }

    public String convert() {
        String obj = m.group(1);
        String a = m.group(2);
        String b = m.group(3);
        String prop = m.group(4);
        String args = m.group(5).trim();

        String x = checkBuildInProps(obj, a, b, prop, args);
        //if (x != null) return x;
        return x;

        /*
        //fuction call?
        String line = m.group(0);
        if (box.callProcessor.test(line)) {

        }

        return ret;
        */
    }

    private String checkBuildInProps(String obj, String a, String b, String prop, String args) {
        if (prop.equals("map")) {
            mapMatcher = map.matcher(args);
            if (mapMatcher.find()) {
                return mapFunction(obj, a, b);
            }
        }

        if (prop.equals("length")) {

        }

        return null;
    }

    private String mapFunction(String obj, String a, String b) {
        String variable = mapMatcher.group(1);
        String content = mapMatcher.group(2);

        //prepare for-loop
        String line = "";
        if (a == null || b == null) {

            String name = obj;

            //array inserted directly? extract first.
            if (obj.contains("[")) {
                name = box.compiler.generateRandomName();
                box.arrayProcessor.testNormal(obj);
                line += box.arrayProcessor.convertArrayNormal(name, false);
            }

            //size and type
            Integer arraySize = box.compiler.getArraySize(name);
            String size = (arraySize != null) ? String.valueOf(arraySize) : "-1";

            //for loop
            String i = box.compiler.generateRandomName();
            line += "for (int " + i + " = 0; " + i + " < " + size + ";" + i + "++) {\n";

            //variable access in array
            String s = name + "[" + i + "]";
            if (box.compiler.getType(name) == Type.ARRAY) {
                box.arrayProcessor.testArrayRead(s);
                s = box.arrayProcessor.convertArrayRead(variable, s);
            }
            line += s;

        } else {
            //range
            //increasing og decreasing range?
            int aValue;
            int bValue;
            try {
                aValue = (a.matches("\\d+")) ? Integer.valueOf(a) : box.compiler.getVariableValue(a);
                bValue = (b.matches("\\d+")) ? Integer.valueOf(b) : box.compiler.getVariableValue(b);
            } catch (NullPointerException e) {
                aValue = 0;
                bValue = 1;
            }

            if (aValue < bValue) {
                line = "for (int " + variable + " = " + a + "; " + variable + " <= " + b + "; " + variable + "++) {\n";
            } else {
                line = "for (int " + variable + " = " + a + "; " + variable + " >= " + b + "; " + variable + "--) {\n";
            }
        }

        //parse body
        box.compiler.increaseScopeLevel();
        String processedContent = box.compiler.processString(content);
        processedContent += box.compiler.getFreeStrings();

        line += processedContent;
        line += "}\n";
        return line;
    }


}
