package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StringProcessor {

    Pattern stringPattern = Pattern.compile("^\\s*\"([^\"]*)\"\\s*(const)?\\s*$");
    Pattern stringCat = Pattern.compile("^\\s*(?:[\\w\\[\\]]+|\".*\")(?:\\s*~\\s*(?:[\\w\\[\\]]+|\".*\"))+");
    Pattern empty = Pattern.compile("^\\s*(string)\\s*\\(\\s*(\\d+)\\s*\\)");

    Matcher stringMatcher;
    Matcher catMatcher;
    Matcher emptyMatcher;

    Box box;

    public StringProcessor(Box box) {
        this.box = box;
    }

    public boolean testString (String s) {
        stringMatcher = stringPattern.matcher(s);
        return stringMatcher.find();
    }

    public boolean testStringCat (String s) {
        catMatcher = stringCat.matcher(s);
        return catMatcher.find();
    }

    public boolean testEmpty (String s) {
        emptyMatcher = empty.matcher(s);
        return emptyMatcher.find();
    }

    public String convertEmpty(String name) {

        if (name == null) name = box.compiler.generateRandomName();
        int size = Integer.parseInt(emptyMatcher.group(2));
        String line = name + " = (char *) malloc (" + size + ");\n";
        box.compiler.addFreeString(name,"free(" + name + ");\n");

        return line;
    }

    public String convertString(String name,String content) {

        if (name == null) name = box.compiler.generateRandomName();
        if (content == null) content = stringMatcher.group(1);
        int size = content.length();

        //constant string
        if (stringMatcher.group(2) != null) {
            String line = "char *" + name + " = \"" + content + "\";\n";
            return line;
        }

        String line = "char *" + name + " = (char *) malloc (" + size + ");\n";
        line += "strcpy(" + name + ", \"" + content + "\");\n";

        /*
        if (name == null && content == null) {
            //anonymous string
            line += name;
        }*/
        box.compiler.addFreeString(name,"free(" + name + ");\n");

        return line;
    }

    public String convertStringCat (String name, boolean dynamic) {

        String content = catMatcher.group(0).trim();
        String[] tokens = content.split("~");
        String size = "";
        String before = "";

        //find length of each item
        for (int i = 0; i < tokens.length; i++) {
            String tok = tokens[i].trim();

            Matcher matcher = stringPattern.matcher(tok);
            if (matcher.find()) {
                //inline string
                size += matcher.group(1).length() + "+";
            } else if (box.arrayProcessor.testArrayRead(tok)) {
                //array read
                String rname1 = box.compiler.generateRandomName();
                before += box.arrayProcessor.convertArrayRead(rname1,tok);

                //duplicate code of else-else-case
                String rname2 = box.compiler.generateRandomName();
                before += "char "+rname2+"[12];\n";
                before += "snprintf("+rname2+", 12, \"%d\", "+rname1+");\n";
                size += "strlen("+rname2+")+";
                tokens[i] = rname2;

            } else {
                if (box.compiler.getType(tok) == Type.STRING) {
                    //string variable
                    size += "strlen("+tok+")+";
                } else {
                    //integer
                    String rname = box.compiler.generateRandomName();
                    before += "char "+rname+"[12];\n";
                    before += "snprintf("+rname+", 12, \"%d\", "+tok+");\n";
                    size += "strlen("+rname+")+";
                    tokens[i] = rname;
                }
            }
        }
        //remove trailing +
        size = size.substring(0,size.length()-1);

        String line = before;
        if (dynamic) {
            String free = box.compiler.removeOneFreeString(name);
            if (free != null) line += free;
        } else {
            line += "char *";
        }

        //create umbrella string and add each token
        line += name + " = (char *) malloc ("+size+");\n";
        for (String tk : tokens) {
            line += "strcat("+name+", "+tk+");\n";
        }

        box.compiler.addFreeString(name,"free("+name+");\n");
        return line;
    }
}
