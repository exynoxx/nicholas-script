package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ArrayProcessor {

    Pattern empty = Pattern.compile("^\\s*array\\s*\\(\\s*(\\d+)\\s*,\\s*(\\w+)\\s*\\)");
    Pattern range = Pattern.compile("^\\s*(\\d+|\\w+)\\.\\.(\\d+|\\w+)\\s*(const)?");
    Pattern slice = Pattern.compile("^\\s*(\\w+)\\[\\s*(\\d+)\\s*:\\s*(\\d+)\\s*\\]");
    Pattern normal = Pattern.compile("^\\s*\\[(.*)\\]\\s*(const)?");
    Pattern arrayRead = Pattern.compile("^\\s*(\\w+)\\s*\\[(\\d+|\\w+)\\]");
    Matcher normalMatcher;
    Matcher rangeMatcher;
    Matcher emptyMatcher;
    Matcher readMatcher;
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

    public boolean testArrayRead (String s) {
        readMatcher = arrayRead.matcher(s);
        return readMatcher.find();
    }

    public String convertArrayRead (String assigneeName, String s) {
        String name = readMatcher.group(1);
        Type t = box.compiler.getArrayType(name);
        //this is one array element
        box.compiler.insertType(assigneeName,t);
        if (t == Type.INTEGER) {
            return "*((int *)"+s+")";
        } else if (t == Type.DOUBLE) {
            return "*((double *)"+s+")";
        } else {
            return "((int *)"+s+")";
        }
    }

    /*
    public String convert(String s) {
        String name = arrayAssignmentMatcher.group(1);
        String index = arrayAssignmentMatcher.group(2);
        String value = arrayAssignmentMatcher.group(3);
        String type = null;

        Type t = box.compiler.getArrayType(name);
        if (t == Type.INTEGER) type = "(int *)";
        if (t == Type.DOUBLE) type = "(double *)";
        if (t == Type.STRING) type = "(char *)";

        //((int *) array)[0] = 456;
        String line = "(" + type + name + ")" + index + "=" + value + ";\n";
        return line;
    }
    */

    public String convertArrayEmpty(String name, boolean dynamic) {
        String size = emptyMatcher.group(1);
        String type = emptyMatcher.group(2);
        box.compiler.insertArraySize(name, Integer.parseInt(size));

        String sizeString = null;
        if (type.equals("int") || type.equals("integer")) {
            sizeString = size + "*sizeof(int)";
            box.compiler.insertArrayType(name, Type.INTEGER);
        }

        if (type.equals("string")) {
            sizeString = size + "*sizeof(char)";
            box.compiler.insertArrayType(name, Type.STRING);
        }

        if (type.equals("double")) {
            sizeString = size + "*sizeof(double)";
            box.compiler.insertArrayType(name, Type.DOUBLE);
        }

        String ret = (dynamic) ? "free(" + name + ");\n" : "";
        ret += "void **" + name + " = (void **) malloc (" + sizeString + ");\n";
        box.compiler.addFreeString("free(" + name + ");\n");
        return ret;
    }

    public String convertArrayNormal(String name, boolean dynamic) {
        boolean constant = (normalMatcher.group(2) == null) ? false : true;

        String arrayContent = normalMatcher.group(1).trim();
        String[] elements = arrayContent.split(",");
        String firstElement = elements[0];
        int size = elements.length;
        String typeString = null;

        if (firstElement.contains("\"")) {
            box.compiler.insertArrayType(name, Type.STRING);
            typeString = "char *";
        } else if (firstElement.matches("\\d+\\.\\d+")) {
            box.compiler.insertArrayType(name, Type.DOUBLE);
            typeString = "double ";
        } else {
            box.compiler.insertArrayType(name, Type.INTEGER);
            typeString = "int ";
        }

        if (constant) {
            box.compiler.insertArraySize(name, size);
            return typeString + name + "[] = {" + arrayContent + "};\n";
        } else {
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
            if (typeString.equals("char *")) {
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

        box.compiler.insertArrayType(name, Type.INTEGER);
        //type = 0:int 1:double 2:string

        int from = (a.matches("\\d+")) ? Integer.parseInt(a) : box.compiler.getVariableValue(a);
        int to = (b.matches("\\d+")) ? Integer.parseInt(b) : box.compiler.getVariableValue(b);
        int size = Math.abs(to - from) + 1;
        String comparator = "<=";
        String incrementer = "++";
        if (from > to) {
            //int tmp = to;
            //to = from;
            //from = tmp;
            comparator = ">=";
            incrementer = "--";
        }
        box.compiler.insertArraySize(name, size);

        if (constant) {
            String line = "int " + name + "[] = {";
            if (to > from) {
                for (int i = from; i < to; i++) {
                    line += i + ",";
                }
                line += to + "};\n";
            } else {
                for (int i = to; i > from; i++) {
                    line += i + ",";
                }
                line += from + "};\n";
            }
            return line;
        } else {
            String malLine = "";

            if (dynamic) {
                malLine += "for (int i = 0; i < " + box.compiler.getArraySize(name) + ";i++) free (" + name + "[i]);\n";
                malLine += "free(" + name + ");\n";
                malLine += "void **";
            }
            box.compiler.insertArraySize(name, size);

            malLine += "void **" + name + " = (void **) malloc (" + size + "*sizeof(void *));\n";
            //*((int *)(arr[1])) = 5;
            String nextline = "for (int i = " + from + ", j = 0; i" + comparator + to + "; i" + incrementer + ", j++) {\n";
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
