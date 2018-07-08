package compiler;

import compiler.Compiler;
import compiler.Processor;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class PropertyProcessor implements Processor {

    Pattern range;
    Matcher m;
    boolean debug;
    Pattern property;

    Compiler compiler;
    public PropertyProcessor(Compiler compiler, boolean debug) {
        this.debug = debug;
        this.compiler = compiler;
        property = Pattern.compile("(\\d+\\.\\.\\d+|\\w+)\\.(\\w+)(.*)");
        range = Pattern.compile("(\\d+)\\.\\.(\\d+)");
        //mapPattern = Pattern.compile("\\s*\\((\\w+)\\)\\s*(?:->|=>)\\s*(.*)");
    }

    @Override
    public boolean test(String s) {
        m = property.matcher(s);
        return m.find();
    }

    @Override
    public String convert(String s) {
        if (debug) System.out.println("-- property");

        String objName = m.group(1);
        String propertyName = m.group(2);
        String rest = m.group(3);

        Matcher localMatcher = range.matcher(objName);
        if (localMatcher.find()) {

        } else {

        }



        /*
        String line = "for(int "+ m.group(1).trim() +" = 0; "+ m.group(1).trim() +" < " + processLength(var) + " ; "+ m.group(1).trim() +"++){\n";
        line += processString(m.group(2).trim(),1) + "\n";
        line+="}";
        */
        return null;
    }
}
