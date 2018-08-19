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

    public boolean testArrayRead(String s) {
        readMatcher = arrayRead.matcher(s);
        return readMatcher.find();
    }

    public String convertArrayRead(String assigneeName, String s) {
        String name = readMatcher.group(1);
        Type t = box.compiler.getArrayType(name);

        String pre = null;
        String ret = null;

        if (t == Type.INTEGER) {
            pre = "int ";
            ret = "*((int *)" + s + ")";
        } else if (t == Type.DOUBLE) {
            pre = "double ";
            ret = "*((double *)" + s + ")";
        } else {
            pre = "char *";
            ret = "((char *)" + s + ")";
        }

        if (assigneeName != null) {
            box.compiler.insertType(assigneeName, t);        //this is one array element
            return pre+assigneeName+" = "+ret+";\n";
        }

        return ret;
    }

    public String convertArrayWrite (String name, String arrayIndex, String assignee) {
        Type t = box.compiler.getArrayType(name);

        if (t == Type.INTEGER) {
            return "*((int *)" + name + arrayIndex + ") = " + assignee + ";\n";
        } else if (t == Type.DOUBLE) {
            return "*((double *)" + name + arrayIndex + ") = " + assignee + ";\n";
        } else {
            return "((char *)" + name + arrayIndex + ") = " + assignee + ";\n";
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

        String ret = null;
        if (dynamic) {
            ret = box.compiler.getOneFreeString(name);
        } else {
            ret = "void **";
        }

        ret += name + " = (void **) malloc (" + sizeString + ");\n";
        box.compiler.addFreeString(name, "free(" + name + ");\n");
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
            typeString = "double";
        } else {
            box.compiler.insertArrayType(name, Type.INTEGER);
            typeString = "int";
        }

        box.compiler.insertArraySize(name, size);
        if (constant) {
            box.compiler.insertType(name, Type.CONSTARRAY); //override assignment write
            return typeString + " " + name + "[] = {" + arrayContent + "};\n";
        } else {
            box.compiler.insertType(name, Type.ARRAY); //override assignment write
            String malLine = "";
            if (dynamic) {
                malLine += box.compiler.getOneFreeString(name);
            } else {
                malLine += "void **";
            }
            malLine += name + " = (void **) malloc (" + size + "*sizeof(void *));\n";

            String free = "for (int i = 0; i < " + size + ";i++) free (" + name + "[i]);\nfree(" + name + ");\n";
            box.compiler.addFreeString(name, free);
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
            box.compiler.insertType(name, Type.CONSTARRAY); //override assignment write
            String line = "int " + name + "[] = {";
            if (to > from) {
                //increasing range
                for (int i = from; i < to; i++) line += i + ",";
            } else {
                //decreasing range
                for (int i = from; i > to; i--) line += i + ",";
            }
            line += to + "};\n";
            return line;
        } else {
            String malLine = "";

            if (dynamic) {
                malLine += box.compiler.getOneFreeString(name);
            } else {
                malLine += "void **";
            }

            String i = box.compiler.generateRandomName();
            String j = box.compiler.generateRandomName();

            malLine += name + " = (void **) malloc (" + size + "*sizeof(void *));\n";
            String nextline = "for (int "+i+" = " + from + ", "+j+" = 0;"+ i + comparator + to + ";" + i + incrementer + ","+j+"++) {\n";
            nextline += name + "["+j+"] = malloc(sizeof(int));\n";
            nextline += "*((int *)(" + name + "["+j+"])) = "+i+";\n";
            nextline += "}\n";

            String free = "for (int i = 0; i < " + size + ";i++) free (" + name + "[i]);\nfree(" + name + ");\n";
            box.compiler.addFreeString(name, free);

            return malLine + nextline;
        }


    }
}
