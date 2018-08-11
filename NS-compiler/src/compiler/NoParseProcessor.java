package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class NoParseProcessor{

    Pattern noParseCode = Pattern.compile("^\\s*\\(c\\)\\s*\\{(.*)\\}");
    Matcher m;
    Box box;

    public NoParseProcessor(Box box) {
        this.box = box;
    }

    public boolean test(String s) {
        m = noParseCode.matcher(s);
        return m.find();
    }

    public String convert(String s) {
        return m.group(1).trim();
    }
}
