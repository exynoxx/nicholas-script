package AssignmentUtil;

import compiler.Compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ArrayProcessor {

    Pattern empty;
    Pattern range;
    Pattern slice;
    Pattern normal;
    boolean debug;
    Compiler compiler;
    Matcher normalMatcher;
    Matcher rangeMatcher;
    Matcher emptyMatcher;
    String retType = "int *";

    public ArrayProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        range = Pattern.compile("^\\s*(\\d+)\\.\\.(\\d+)\\s*(const)?");
        slice = Pattern.compile("^\\s*(\\w+)\\[\\s*(\\d+)\\s*:\\s*(\\d+)\\s*\\]");
        normal = Pattern.compile("^\\s*\\[(.*)\\]\\s*(const)?");
        empty = Pattern.compile("^\\s*(int|string)\\s*\\(\\s*(\\d+)\\s*\\)");
    }

    public boolean testNormal (String s) {
        normalMatcher = normal.matcher(s);
        return normalMatcher.find();
    }

    public boolean testRange (String s) {
        rangeMatcher = range.matcher(s);
        return rangeMatcher.find();
    }

    public boolean testEmpty (String s) {
        emptyMatcher = empty.matcher(s);
        return emptyMatcher.find();
    }

    public String convert(String name, String s, int type, boolean dynamic) {
        String ret = null;

        if (type == 0) {
            ret = arrayNormal(name,dynamic);
        } else if (type == 1) {
            ret = arrayRange(name,dynamic);
        } else {
            ret = arrayEmpty(name,dynamic);
        }

        if (!dynamic) {
            ret = retType + ret;
        }

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        }

        return ret;
    }

    private String arrayEmpty (String name, boolean dynamic) {
        String type = emptyMatcher.group(1);
        String size = emptyMatcher.group(2);

        retType = "int *";
        String ret = (dynamic) ? "free("+name+");\n" : "";
        ret += name + " = (int *) malloc (" + size + "*sizeof(int));\n";
        if (!dynamic) compiler.addFreeString("free("+name+");\n");
        return ret;
    }

    private String arrayNormal(String name, boolean dynamic) {
        boolean constant = (normalMatcher.group(2) == null) ? false : true;

        String arrayContent = normalMatcher.group(1).trim();
        if (constant) {
            retType = "int ";
            return name + "[] = {" + arrayContent + "};\n";
        } else {
            retType = "int *";
            String c = arrayContent.replaceAll("\\s+", "");
            int size = c.split(",").length;
            String malLine = (dynamic)? "free("+name+");\n" : "";
            malLine += name + " = (int *) malloc (" + size + "*sizeof(int));\n";
            if (!dynamic) compiler.addFreeString("free("+name+");\n");
            String[] elements = arrayContent.split(",");
            String nextline = "";
            for (int i = 0; i < elements.length; i++) {
                nextline += name + "["+i+"] = " + elements[i] + ";\n";
            }
            return malLine + nextline;
        }
    }


    private String arrayRange(String name, boolean dynamic) {
        boolean constant = (rangeMatcher.group(3) == null) ? false : true;

        int from = Integer.parseInt(rangeMatcher.group(1));
        int to = Integer.parseInt(rangeMatcher.group(2));

        if (constant) {

            retType = "int ";
            String line = name + "[] = {";
            for (int i = from; i < to; i++) {
                line += i + ",";
            }
            line += to + "};\n";
            return line;
        } else {
            retType = "int *";
            int size = to-from+1;
            String malLine = (dynamic)? "free("+name+");\n" : "";
            malLine += name + " = (int *) malloc (" + size + "*sizeof(int));\n";
            if (!dynamic) compiler.addFreeString("free("+name+");\n");
            String nextline = "for (int i = " + from + ", j = 0; i <= " + to + "; i++, j++)  " + name + "[j] = i;\n"; //name + " = (int["+size+"]){";
            return malLine+nextline;
        }


    }
}
