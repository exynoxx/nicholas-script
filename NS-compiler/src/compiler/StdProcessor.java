package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StdProcessor {

    Pattern stdin;
    Matcher m;
    Box box;

    public StdProcessor(Box box) {
        this.box = box;
        stdin = Pattern.compile("^\\s*stdin:\\s*((?:\\w\\s*)*)");
    }

    public boolean test(String s) {
        m = stdin.matcher(s);
        return m.find();
    }

    public String convert(String s) {

        String args = m.group(1);
        String[] tokens = args.trim().split("\\s+");
        String scanfString = " ";
        String argString = "";

        for (String t : tokens) {
            if (box.compiler.getType(t) == Type.STRING) {
                scanfString += "%s";
                argString += t + ",";
            } else {
                argString += "&" + t + ",";
                if (box.compiler.getType(t) == Type.INTEGER)
                    scanfString += "%d";
                else
                    scanfString += "%lf";
            }
        }

        String line = "scanf (\"" + scanfString + "\", " + argString.substring(0, argString.length() - 1) + ");\n";
        return line;
    }
}
