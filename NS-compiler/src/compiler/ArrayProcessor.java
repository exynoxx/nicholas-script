package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class ArrayProcessor{

    Pattern arrayAssignment;
    Pattern empty;
    Pattern range;
    Pattern slice;
    Pattern normal;
    Matcher normalMatcher;
    Matcher rangeMatcher;
    Matcher emptyMatcher;
    Matcher arrayAssignmentMatcher;
    Box box;

    public ArrayProcessor(Box box) {
        this.box = box;

        range = Pattern.compile("^\\s*(\\d+|\\w+)\\.\\.(\\d+|\\w+)\\s*(const)?");
        slice = Pattern.compile("^\\s*(\\w+)\\[\\s*(\\d+)\\s*:\\s*(\\d+)\\s*\\]");
        normal = Pattern.compile("^\\s*\\[(.*)\\]\\s*(const)?");
        empty = Pattern.compile("^\\s*array\\s*\\(\\s*(\\d+)\\s*,\\s*(\\w+)\\s*\\)");
        arrayAssignment = Pattern.compile("^\\s*([\\w\\.]+)\\s*(\\[\\d+\\])\\s*=\\s*(.*)");
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

    public boolean test(String s) {
        arrayAssignmentMatcher = arrayAssignment.matcher(s);
        return arrayAssignmentMatcher.find();
    }

    public String convert (String s) {
        String name = arrayAssignmentMatcher.group(1);
        String index = arrayAssignmentMatcher.group(2);
        String value = arrayAssignmentMatcher.group(3);
        String type = null;

        int t = compiler.getArrayType(name);
        if (t == 0) {
            type = "(int*)";
        }
        if (t == 1) {
            type = "(double*)";
        }
        if (t == 2) {
            type = "(nstring*)";
        }

        //((int *) array)[0] = 456;
        String line = "("+type+name+")"+index+"="+value+";\n";

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }

        return line;
    }

    public String arrayAssignment(String name, String s, int type, boolean dynamic) {
        String ret = null;

        if (type == 0) {
            ret = arrayNormal(name, dynamic);
        } else if (type == 1) {
            ret = arrayRange(name, dynamic);
        } else {
            ret = arrayEmpty(name, dynamic);
        }

        if (!dynamic) {
            ret = "void **" + ret;
        }

        /*
        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(ret);
        } */

        return ret;
    }

    private String arrayEmpty(String name, boolean dynamic) {
        String size = emptyMatcher.group(1);
        String type = emptyMatcher.group(2);
        compiler.insertArraySize(name, Integer.parseInt(size));

        String sizeString = null;
        if (type.equals("int")||type.equals("integer")) {
            sizeString = size + "*sizeof(int)";
        }

        if (type.equals("string")) {
            sizeString = size + "*sizeof(nstring)";
        }

        if (type.equals("double")) {
            sizeString = size + "*sizeof(double)";
        }

        String ret = (dynamic) ? "free(" + name + ");\n" : "";
        ret += name + " = (void **) malloc ("+sizeString+");\n";
        compiler.addFreeString("free(" + name + ");\n");
        return ret;
    }

    private String arrayNormal(String name, boolean dynamic) {
        boolean constant = (normalMatcher.group(2) == null) ? false : true;

        String arrayContent = normalMatcher.group(1).trim();
        String firstElement = arrayContent.split(",")[0];
        int type = 0;

        if (firstElement.contains("\"")){
            type = 2;//"const char *";
        } else if (firstElement.matches("\\d+\\.\\d+")){
            type = 1;
        }
        compiler.insertArrayType(name,type);


        String typeString = null;
        if (type == 0) typeString = "int ";
        if (type == 1) typeString = "double ";
        if (type == 2) typeString = "const char *";

        if (constant) {
            int size = arrayContent.split(",").length;
            compiler.insertArraySize(name, size);
            return type + name + "[] = {" + arrayContent + "};\n";
        } else {
            String[] elements = arrayContent.split(",");
            int size = elements.length;

            String malLine = "";
            if (dynamic) {
                malLine += "for (int i = 0; i < "+ compiler.getArraySize(name) + ";i++) free (" + name +"[i]);\n";;
                malLine += "free(" + name + ");\n";
            }
            compiler.insertArraySize(name, size);

            malLine += name + " = (void **) malloc (" + size + "*sizeof(void *));\n";

            String free = "for (int i = 0; i < "+ size + ";i++) free (" + name +"[i]);\n";
            compiler.addFreeString(free);
            if (!dynamic) compiler.addFreeString("free(" + name + ");\n");
            String nextline = "";

            //strings are special
            if (type == 2) {
                for (int i = 0; i < elements.length; i++) {
                    nextline += name + "[" + i + "] = (nstring *) malloc (sizeof(nstring));\n";
                    nextline += "((nstring *)("+name+"["+i+"]))->data = (char *) malloc ("+size+");\n";
                    nextline += "((nstring *)("+name+"["+i+"]))->size = " + size + ";\n";
                    nextline += "strcpy(((nstring *)("+name+"["+i+"])), " + elements[i] + ");\n";
                }

            } else {
                for (int i = 0; i < elements.length; i++) {
                    nextline += name + "[" + i + "] = malloc(sizeof("+typeString+"));\n";
                    nextline += "*(("+typeString+"*)("+name + "[" + i + "])) = " + elements[i] + ";\n";
                }
            }

            return malLine + nextline;
        }
    }


    private String arrayRange(String name, boolean dynamic) {
        boolean constant = (rangeMatcher.group(3) == null) ? false : true;

        String a = rangeMatcher.group(1);
        String b = rangeMatcher.group(2);

        compiler.insertArrayType(name,0);
        //type = 0:int 1:double 2:string


        if (constant) {
            int to = 1;
            int from = 3;

            if (a.matches("\\d+") && b.matches("\\d+")) {
                from = Integer.parseInt(a);
                to = Integer.parseInt(b);
            }
            int size = to - from + 1;
            compiler.insertArraySize(name, size);

            String line = "int " + name + "[] = {";
            for (int i = from; i < to; i++) {
                line += i + ",";
            }
            line += to + "};\n";
            return line;
        } else {

            int from, to;
            from = (a.matches("\\d+")) ? Integer.parseInt(a) : compiler.getVariableValue(a);
            to = (b.matches("\\d+")) ? Integer.parseInt(b) : compiler.getVariableValue(b);
            int size = to - from + 1;
            String malLine = "";

            if (dynamic) {
                malLine += "for (int i = 0; i < "+ compiler.getArraySize(name) + ";i++) free (" + name +"[i]);\n";;
                malLine += "free(" + name + ");\n";
            }
            compiler.insertArraySize(name, size);

            malLine += name + " = (void **) malloc ((" + b + "-" + a + "+1)*sizeof(void *));\n";
            //*((int *)(arr[1])) = 5;
            String nextline = "for (int i = " + a + ", j = 0; i <= " + b + "; i++, j++) {\n";
            nextline += name+"[j] = malloc(sizeof(int));\n";
            nextline += "*((int *)(" + name + "[j])) = i;\n";
            nextline += "}\n";

            String free = "for (int i = 0; i < "+ size + ";i++) free (" + name +"[i]);\n";
            compiler.addFreeString(free);
            if (!dynamic) compiler.addFreeString("free(" + name + ");\n");

            return malLine + nextline;
        }


    }
}
