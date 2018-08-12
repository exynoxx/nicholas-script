package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ArrayProcessor {

    Pattern empty = Pattern.compile("^\\s*array\\s*\\(\\s*(\\d+)\\s*,\\s*(\\w+)\\s*\\)");
    Pattern range = Pattern.compile("^\\s*(\\d+|\\w+)\\.\\.(\\d+|\\w+)\\s*(const)?");
    Pattern slice = Pattern.compile("^\\s*(\\w+)\\[\\s*(\\d+)\\s*:\\s*(\\d+)\\s*\\]");
    Pattern normal = Pattern.compile("^\\s*\\[(.*)\\]\\s*(const)?");
    Matcher normalMatcher;
    Matcher rangeMatcher;
    Matcher emptyMatcher;
    Matcher arrayAssignmentMatcher;
    Box box;

    public ArrayProcessor(Box box) {
        this.box = box;
    }

    public boolean testNormal(String s) {
        normalMatcher = normal.matcher(s);
        return normalMatcher.find();
    }

    public boolean testRange(String s) {
        rangeMatcher = range.matcher(s);
        return rangeMatcher.find();
    }

    public boolean testEmpty(String s) {
        emptyMatcher = empty.matcher(s);
        return emptyMatcher.find();
    }

    public String convert(String s) {
        String name = arrayAssignmentMatcher.group(1);
        String index = arrayAssignmentMatcher.group(2);
        String value = arrayAssignmentMatcher.group(3);
        String type = null;

        int t = box.compiler.getArrayType(name);
        if (t == 0) {
            type = "(int*)";
        }
        if (t == 1) {
            type = "(double*)";
        }
        if (t == 2) {
            type = "(char*)";
        }

        //((int *) array)[0] = 456;
        String line = "(" + type + name + ")" + index + "=" + value + ";\n";
        return line;
    }

    public String arrayAssignment(String name, String s, int type, boolean dynamic) {
        String ret = null;

        if (type == 0) {
            ret = convertArrayNormal(name, dynamic);
        } else if (type == 1) {
            ret = convertArrayRange(name, dynamic);
        } else {
            ret = convertArrayEmpty(name, dynamic);
        }

        if (!dynamic) {
            ret = "void **" + ret;
        }

        return ret;
    }

    public String convertArrayEmpty(String name, boolean dynamic) {
        String size = emptyMatcher.group(1);
        String type = emptyMatcher.group(2);
        box.compiler.insertArraySize(name, Integer.parseInt(size));

        String sizeString = null;
        if (type.equals("int") || type.equals("integer")) {
            sizeString = size + "*sizeof(int)";
        }

        if (type.equals("string")) {
            sizeString = size + "*sizeof(char)";
        }

        if (type.equals("double")) {
            sizeString = size + "*sizeof(double)";
        }

        String ret = (dynamic) ? "free(" + name + ");\n" : "";
        ret += name + " = (void **) malloc (" + sizeString + ");\n";
        box.compiler.addFreeString("free(" + name + ");\n");
        return ret;
    }

    public String convertArrayNormal(String name, boolean dynamic) {
        boolean constant = (normalMatcher.group(2) == null) ? false : true;

        String arrayContent = normalMatcher.group(1).trim();
        String firstElement = arrayContent.split(",")[0];
        int type = 0;

        if (firstElement.contains("\"")) {
            type = 2;//"const char *";
        } else if (firstElement.matches("\\d+\\.\\d+")) {
            type = 1;
        }
        box.compiler.insertArrayType(name, type);


        String typeString = null;
        if (type == 0) typeString = "int ";
        if (type == 1) typeString = "double ";
        if (type == 2) typeString = "const char *";

        if (constant) {
            int size = arrayContent.split(",").length;
            box.compiler.insertArraySize(name, size);
            return type + name + "[] = {" + arrayContent + "};\n";
        } else {
            String[] elements = arrayContent.split(",");
            int size = elements.length;

            String malLine = "";
            if (dynamic) {
                malLine += "for (int i = 0; i < " + box.compiler.getArraySize(name) + ";i++) free (" + name + "[i]);\n";
                malLine += "free(" + name + ");\n";
            }
            box.compiler.insertArraySize(name, size);

            malLine += "void **" + name + " = (void **) malloc (" + size + "*sizeof(void *));\n";

            String free = "for (int i = 0; i < " + size + ";i++) free (" + name + "[i]);\n";
            box.compiler.addFreeString(free);
            if (!dynamic) box.compiler.addFreeString("free(" + name + ");\n");
            String nextline = "";

            //strings are special
            if (type == 2) {
                for (int i = 0; i < elements.length; i++) {
                    nextline += name + "[" + i + "] = (char *) malloc (" + size + ");\n";
                    nextline += "strcpy(((char *)(" + name + "[" + i + "])), " + elements[i] + ");\n";
                }

            } else {
                for (int i = 0; i < elements.length; i++) {
                    nextline += name + "[" + i + "] = malloc(sizeof(" + typeString + "));\n";
                    nextline += "*((" + typeString + "*)(" + name + "[" + i + "])) = " + elements[i] + ";\n";
                }
            }

            return malLine + nextline;
        }
    }


    public String convertArrayRange(String name, boolean dynamic) {
        boolean constant = (rangeMatcher.group(3) == null) ? false : true;

        String a = rangeMatcher.group(1);
        String b = rangeMatcher.group(2);

        box.compiler.insertArrayType(name, 0);
        //type = 0:int 1:double 2:string

        int from = (a.matches("\\d+")) ? Integer.parseInt(a) : box.compiler.getVariableValue(a);
        int to = (b.matches("\\d+")) ? Integer.parseInt(b) : box.compiler.getVariableValue(b);
        int size = Math.abs(to - from + 1);
        if (from > to) {
            int tmp = to;
            to = from;
            from = tmp;
        }
        box.compiler.insertArraySize(name, size);

        if (constant) {
            String line = "int " + name + "[] = {";
            for (int i = from; i < to; i++) {
                line += i + ",";
            }
            line += to + "};\n";
            return line;
        } else {
            String malLine = "";

            if (dynamic) {
                malLine += "for (int i = 0; i < " + box.compiler.getArraySize(name) + ";i++) free (" + name + "[i]);\n";
                malLine += "free(" + name + ");\n";
                malLine += "void **";
            }
            box.compiler.insertArraySize(name, size);

            malLine += name + " = (void **) malloc ((" + b + "-" + a + "+1)*sizeof(void *));\n";
            //*((int *)(arr[1])) = 5;
            String nextline = "for (int i = " + a + ", j = 0; i <= " + b + "; i++, j++) {\n";
            nextline += name + "[j] = malloc(sizeof(int));\n";
            nextline += "*((int *)(" + name + "[j])) = i;\n";
            nextline += "}\n";

            String free = "for (int i = 0; i < " + size + ";i++) free (" + name + "[i]);\n";
            box.compiler.addFreeString(free);
            if (!dynamic) box.compiler.addFreeString("free(" + name + ");\n");

            return malLine + nextline;
        }


    }
}
