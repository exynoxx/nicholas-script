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

            Matcher matcher = map.matcher(args);
            String variable = m.group(1);
            String content = m.group(2);
            String processedContent = null;
            for (String s : box.compiler.tokenize(content)) {
                processedContent += s;
            }

            //TODO: parse content

            String line = null;
            if (a == null || b == null) {
                //variable.map: i -> ....;

                Integer arraySize = box.compiler.getArraySize(obj);
                String size = (arraySize != null) ? String.valueOf(arraySize) : "-1";

                String newVar = box.compiler.generateRandomName();
                line = "for (int " + newVar + " = 0; " + newVar + " < " + size + ";" + newVar + "++) {\n";
                line += "int " + variable + " = " + obj + "[" + newVar + "];\n";

            } else {
                //increasing og decreasing range?
                int aValue = (a.matches("\\d+")) ? Integer.valueOf(a) : box.compiler.getVariableValue(a);
                int bValue = (b.matches("\\d+")) ? Integer.valueOf(b) : box.compiler.getVariableValue(b);

                if (aValue < bValue) {
                    line = "for (int " + variable + " = " + a + "; " + variable + " <= " + b + "; " + variable + "++) {\n";
                } else {
                    line = "for (int " + variable + " = " + a + "; " + variable + " >= " + b + "; " + variable + "--) {\n";
                }
            }
            line += processedContent;
            line += "}\n";
            return line;
        }

        if (prop.equals("length")) {

        }

        return null;
    }


}
