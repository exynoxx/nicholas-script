package compiler;

import java.util.Random;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CallProcessor implements Processor {

    boolean debug;
    Pattern functionCall;
    Matcher m;
    Compiler compiler;
    boolean standalone;
    Random random;
    String before = "";
    String after = "";

    public CallProcessor(Compiler compiler, boolean debug, boolean standalone) {
        this.debug = debug;
        this.compiler = compiler;
        this.standalone = standalone;
        random = new Random();
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
            newArgs += args.substring(lastPos,args.length());
        } else {
            newArgs = args;
        }

        newArgs = newArgs.replaceAll("\\s+", ",");
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
