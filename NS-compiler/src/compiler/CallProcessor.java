package compiler;

import java.util.Random;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CallProcessor  {

    boolean debug;
    Pattern functionCall;
    Matcher m;
    Random random;
    String before = "";
    String after = "";
    Box box;

    public CallProcessor(Box box) {
        this.box = box;
        functionCall = Pattern.compile("^\\s*([\\w\\.]+):(.*)");
    }

    @Override
    public boolean test(String s) {
        m = functionCall.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {
        String name = m.group(1);
        String args = m.group(2).trim();
        String line = recursiveConvert(name, args) + ";\n";
        line = before + line + after;
        before = "";
        after = "";

        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }
        return line;
    }

    private String recursiveConvert(String name, String args) {
        String newArgs = "";
        String stringExtract = "";

        //recursive: function call in args.
        if (args.contains(":")) {
            if (test(args.trim())) {
                args = recursiveConvert(m.group(1),m.group(2).trim());
            }
        }

        //contains expression
        if (!args.contains(":") && args.contains("~") || args.contains("+") || args.contains("*") || args.contains("/")) {
            String tmpName = generateRandomName();
            String nsString = "var " + tmpName + " = " + args.trim() + ";";
            compiler.increaseScopeLevel();
            before = compiler.tokenize(nsString);
            after = compiler.getFreeStrings();
            //after later
            args = tmpName;
        }

        //does arguments contain string? yes: extract it into seperate line of code.
        if(args.contains("\"")){
            char[] charArray = args.toCharArray();
            int scope = 0;
            int lastPos = 0;

            for (int i = 0; i < charArray.length; i++) {

                if (charArray[i] == '"' && scope > 0){
                    scope--;
                    String newName = generateRandomName();
                    //compiler.increaseScopeLevel();
                    String nsString = "string " + newName + " = " + args.substring(lastPos, i + 1) + ";";
                    //string processor will insert statement.
                    //string XXXXX = "hello wolrd";
                    stringExtract = compiler.tokenize(nsString);
                    if (compiler.getScopeLevel() > 0) {
                        before += stringExtract;
                    }
                    newArgs += newName;
                    lastPos = i+1;
                    continue;
                }

                if (charArray[i] == '"' && scope == 0) {
                    scope++;
                    newArgs += args.substring(lastPos,i);
                    lastPos = i;
                }
            }
            args += args.substring(lastPos,args.length());
        }

        if (args.contains("[")) {
            Pattern array = Pattern.compile("([\\w\\.]+)\\s*(\\[\\d+\\])");

            String[] elements = args.split(" ");
            String tmpArgs = "";

            for (String s : elements) {

                Matcher tmpmatcher = array.matcher(s);
                if (tmpmatcher.find()) {
                    int type = compiler.getArrayType(tmpmatcher.group(1));
                    String newElement = null;

                    if (type == 0) {
                        newElement = "*((int *)("+tmpmatcher.group(0)+"))";
                    } else if (type == 1) {
                        newElement = "*((double *)("+tmpmatcher.group(0)+"))";
                    } else {
                        newElement = "((nstring *)("+tmpmatcher.group(0)+"))";

                    }
                    tmpArgs += newElement + " ";
                } else {
                    tmpArgs += s + " ";
                }
            }
            args = tmpArgs;
        }

        newArgs = args.trim();
        //newArgs = newArgs.replaceAll("\\s+", ",");
        return name + "(" + newArgs + ")";
    }

    public String generateRandomName() {

        int leftLimit = 97; // letter 'a'
        int rightLimit = 122; // letter 'z'
        int targetStringLength = 5;

        StringBuilder buffer = new StringBuilder(targetStringLength);
        for (int i = 0; i < targetStringLength; i++) {
            int randomLimitedInt = leftLimit + (int) (random.nextFloat() * (rightLimit - leftLimit + 1));
            buffer.append((char) randomLimitedInt);
        }

        //String generatedString = buffer.toString();
        return buffer.toString();
    }

}
