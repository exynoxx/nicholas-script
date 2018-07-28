package compiler;

import java.util.ArrayList;
import java.util.LinkedList;

public class PreProcessor {

    public String clean(String s) {
        String string = s;
        string = string.replaceAll("^\\s*#.*\\n$", "");
        string = string.replaceAll("\\n", " ");
        //string = string.replaceAll("\\s+", " ");
        return string;
    }

    public LinkedList<String> partition (String s) {
        LinkedList<String> tokens = new LinkedList<>();
        char[] charArray = s.toCharArray();

        int lastIndex = 0;
        int scopeCount = 0;
        for (int i = 0; i < charArray.length; i++) {
            if (charArray[i] == ';' && scopeCount == 0){
                tokens.add(s.substring(lastIndex,i));
                lastIndex = i+1;
            }
            if (charArray[i] == '{') scopeCount++;
            if (charArray[i] == '}') scopeCount--;
        }

        return tokens;
    }

    public String extractGlobalCodeAndReturnRest (String s) {
        int end = s.indexOf("%endglobal");
        if (end < 0) {
            return null;
        } else {
            return s.substring(end+10,s.length());
        }
    }

    public String extractGlobalCode (String s) {
        int start = s.indexOf("%beginglobal");
        int end = s.indexOf("%endglobal");
        if (start < 0 || end < 0) {
            return null;
        } else {
            return s.substring(start+12,end);
        }
    }
}
