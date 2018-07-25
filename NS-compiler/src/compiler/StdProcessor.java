package compiler;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StdProcessor implements Processor {

    boolean debug;
    Compiler compiler;
    Pattern stdin;
    Matcher m;


    public StdProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        stdin = Pattern.compile("stdin: (\\w)");
    }

    @Override
    public boolean test(String s) {
        m = stdin.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {
        String line = "scanf (\"%d\", &" + m.group(1) + ");\n";
        if (compiler.getScopeLevel() == 0) {
            compiler.insertStatement(line);
        }
        return line;
    }
}
