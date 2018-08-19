package compiler;

import java.util.LinkedList;
import java.util.Random;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CallProcessor {

    Pattern functionCall = Pattern.compile("^\\s*([\\w\\.]+):(.*)");
    Matcher m;
    Box box;

    public CallProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = functionCall.matcher(s);
        return m.find();
    }

    public String convert(String assigneeName, boolean recursive) {
        String name = m.group(1);
        String args = m.group(2).trim();
        String before = " ";
        String after = " ";
        Type type = box.compiler.getType(name);

        //recursive function call in args.
        if (args.contains(":")) {
            if (test(args.trim())) {
                String sado = convert(null,true);
                String[] elements = sado.split("#345#");
                before += elements[0];
                args = elements[1];
                after += elements[2];
            }
        } //string cat as arg
        else if (args.contains("~") || args.contains("+") || args.contains("*") || args.contains("/")) {
            String rname = box.compiler.generateRandomName();
            String nsString = "var " + rname + " = " + args.trim();
            box.compiler.increaseScopeLevel();
            before += box.compiler.processString(nsString);
            after += box.compiler.getFreeStrings();
            args = rname;
        }

        //extract strings from args
        if (args.contains("\"")) {
            char[] charArray = args.toCharArray();
            int scope = 0;
            int lastPos = 0;
            String tmpArg = "";

            for (int i = 0; i < charArray.length; i++) {

                if (charArray[i] == '"' && scope > 0) {
                    scope--;
                    String rname = box.compiler.generateRandomName();
                    box.compiler.increaseScopeLevel();
                    String nsString = "string " + rname + " = " + args.substring(lastPos, i + 1);
                    before += box.compiler.processString(nsString);
                    after += box.compiler.getFreeStrings();
                    tmpArg += rname + " ";
                    lastPos = i + 1;
                    continue;
                }

                if (charArray[i] == '"' && scope == 0) {
                    scope++;
                    tmpArg += args.substring(lastPos, i);
                    lastPos = i;
                }
            }
            tmpArg += args.substring(lastPos, args.length());
            args = tmpArg;
        }

        //process arrays in args
        if (args.contains("[")) {
            Pattern array = Pattern.compile("([\\w\\.]+)\\s*(\\[\\d+\\])");

            String[] elements = args.split(" ");
            String tmpArgs = "";

            for (String s : elements) {

                Matcher tmpmatcher = array.matcher(s);
                if (tmpmatcher.find()) {
                    type = box.compiler.getArrayType(tmpmatcher.group(1));
                    String newElement = null;

                    if (type == Type.INTEGER) {
                        newElement = "*((int *)(" + tmpmatcher.group(0) + "))";
                    } else if (type == Type.DOUBLE) {
                        newElement = "*((double *)(" + tmpmatcher.group(0) + "))";
                    } else {
                        //Type.STRING
                        newElement = "((char *)(" + tmpmatcher.group(0) + "))";
                    }
                    tmpArgs += newElement + " ";
                } else {
                    tmpArgs += s + " ";
                }
            }
            args = tmpArgs;
        }
        args = args.trim();
        args = args.replaceAll("\\s+", ",");
        if (recursive) {
            String rtgv = before + "#345#" + name + "(" + args + ")" + "#345#" + after;
            return rtgv;
        } else {

            if (assigneeName != null) {
                if (type == Type.INTEGER) {
                    before += "int ";
                } else if (type == Type.DOUBLE) {
                    before += "double ";
                } else {
                    before += "char *";
                }
                before += assigneeName + " = ";
            }


            return before + name + "(" + args + ");\n" + after;
        }
    }

    /*
    private LinkedList<String> partitionArgs(String args) {
        char[] array = args.toCharArray();

        int sc = 0; //string count;
        int bc = 0; //bracket count
        int ss = 0; //string start;
        for (int i = 0; i < array.length; i++) {

            //string start
            if (sc == 0 || array[i] == '"') {
                sc++;
                ss = i;
            }

            //string end
            if (sc > 0 || array[i] == '"') {
                sc--;
            }

        }

    }*/

}
