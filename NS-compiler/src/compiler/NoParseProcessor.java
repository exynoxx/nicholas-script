package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class NoParseProcessor implements Processor {

    Pattern noParseCode = Pattern.compile("^\\s*\\(c\\)\\s*\\{(.*)\\}");
    Matcher m;
    boolean debug;
    Compiler compiler;

    public NoParseProcessor(Compiler compiler,boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
    }

    @Override
    public boolean test(String s) {
        m = noParseCode.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {
        if (debug) System.out.println("-- no parse");
        return m.group(1).trim();
    }
}
