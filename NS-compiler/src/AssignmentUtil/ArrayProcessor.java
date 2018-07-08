package AssignmentUtil;

import compiler.Compiler;
import compiler.Processor;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ArrayProcessor {

    Pattern range;
    Pattern slice;
    Pattern normal;
    boolean debug;
    Compiler compiler;

    public ArrayProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        range = Pattern.compile("^\\s*(\\d+)\\.\\.(\\d+)\\s*(const)?");
        slice = Pattern.compile("^\\s*(\\w+)\\[\\s*(\\d+)\\s*:\\s*(\\d+)\\s*\\]");
        normal = Pattern.compile("^\\s*\\[(.*)\\]\\s*(const)?");
    }

    public String convert(String name, String s) {

        if (debug) System.out.println("---- array processor");

        Matcher matcher = normal.matcher(s);
        if (matcher.find()) {
            return arrayNormal(name, matcher);
        }

        matcher = range.matcher(s);
        if (matcher.find()) {
            return range(name,matcher);
        }

        return null;
    }

    private String arrayNormal(String name, Matcher matcher) {
        boolean constant = (matcher.group(2) == null) ? false : true;

        String arrayContent = matcher.group(1).trim();
        if (constant) {
            return "int " + name + "[] = {" + arrayContent + "};\n";
        } else {
            String c = arrayContent.replaceAll("\\s+", "");
            int size = c.split(",").length;
            String malLine = "int *" + name + " = (int *) malloc (" + size + "*sizeof(int));\n";
            compiler.addFreeString("free("+name+");\n");
            String[] elements = arrayContent.split(",");
            String nextline = "";
            for (int i = 0; i < elements.length; i++) {
                nextline += name + "["+i+"] = " + elements[i] + ";\n";
            }
            return malLine + nextline;
        }
    }


    private String range(String name, Matcher matcher) {
        boolean constant = (matcher.group(3) == null) ? false : true;

        int from = Integer.parseInt(matcher.group(1));
        int to = Integer.parseInt(matcher.group(2));

        if (constant) {

            String line = "int " + name + "[] = {";
            for (int i = from; i < to; i++) {
                line += i + ",";
            }
            line += to + "};\n";
            return line;
        } else {
            int size = to-from+1;
            String malLine = "int *" + name + " = (int *) malloc (" + size + "*sizeof(int));\n";
            compiler.addFreeString("free("+name+");\n");
            String nextline = "for (int i = " + from + ", j = 0; i <= " + to + "; i++, j++)  " + name + "[j] = i;\n"; //name + " = (int["+size+"]){";
            return malLine+nextline;
        }


    }
}
