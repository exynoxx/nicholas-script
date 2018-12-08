// Generated from Grammar.g4 by ANTLR 4.5.3
import org.antlr.v4.runtime.Lexer;
import org.antlr.v4.runtime.CharStream;
import org.antlr.v4.runtime.Token;
import org.antlr.v4.runtime.TokenStream;
import org.antlr.v4.runtime.*;
import org.antlr.v4.runtime.atn.*;
import org.antlr.v4.runtime.dfa.DFA;
import org.antlr.v4.runtime.misc.*;

@SuppressWarnings({"all", "warnings", "unchecked", "unused", "cast"})
public class GrammarLexer extends Lexer {
	static { RuntimeMetaData.checkVersion("4.5.3", RuntimeMetaData.VERSION); }

	protected static final DFA[] _decisionToDFA;
	protected static final PredictionContextCache _sharedContextCache =
		new PredictionContextCache();
	public static final int
		TYPE=1, STRING=2, LPAREN=3, RPAREN=4, QUOTE=5, LBRACKET=6, RBRACKET=7, 
		PLUS=8, MINUS=9, DIV=10, MULT=11, LE=12, GE=13, LT=14, GT=15, ARROW=16, 
		IF=17, VAR=18, RETURN=19, EQ=20, COMMA=21, NUM=22, ID=23, SEMICOLON=24, 
		COLON=25, WS=26;
	public static String[] modeNames = {
		"DEFAULT_MODE"
	};

	public static final String[] ruleNames = {
		"TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", "RBRACKET", 
		"PLUS", "MINUS", "DIV", "MULT", "LE", "GE", "LT", "GT", "ARROW", "IF", 
		"VAR", "RETURN", "EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", "WS"
	};

	private static final String[] _LITERAL_NAMES = {
		null, null, null, "'('", "')'", "'\"'", "'{'", "'}'", "'+'", "'-'", "'/'", 
		"'*'", "'<='", "'>='", "'<'", "'>'", "'=>'", "'if'", "'var'", "'return'", 
		"'='", "','", null, null, "';'", "':'"
	};
	private static final String[] _SYMBOLIC_NAMES = {
		null, "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", "RBRACKET", 
		"PLUS", "MINUS", "DIV", "MULT", "LE", "GE", "LT", "GT", "ARROW", "IF", 
		"VAR", "RETURN", "EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", "WS"
	};
	public static final Vocabulary VOCABULARY = new VocabularyImpl(_LITERAL_NAMES, _SYMBOLIC_NAMES);

	/**
	 * @deprecated Use {@link #VOCABULARY} instead.
	 */
	@Deprecated
	public static final String[] tokenNames;
	static {
		tokenNames = new String[_SYMBOLIC_NAMES.length];
		for (int i = 0; i < tokenNames.length; i++) {
			tokenNames[i] = VOCABULARY.getLiteralName(i);
			if (tokenNames[i] == null) {
				tokenNames[i] = VOCABULARY.getSymbolicName(i);
			}

			if (tokenNames[i] == null) {
				tokenNames[i] = "<INVALID>";
			}
		}
	}

	@Override
	@Deprecated
	public String[] getTokenNames() {
		return tokenNames;
	}

	@Override

	public Vocabulary getVocabulary() {
		return VOCABULARY;
	}


	public GrammarLexer(CharStream input) {
		super(input);
		_interp = new LexerATNSimulator(this,_ATN,_decisionToDFA,_sharedContextCache);
	}

	@Override
	public String getGrammarFileName() { return "Grammar.g4"; }

	@Override
	public String[] getRuleNames() { return ruleNames; }

	@Override
	public String getSerializedATN() { return _serializedATN; }

	@Override
	public String[] getModeNames() { return modeNames; }

	@Override
	public ATN getATN() { return _ATN; }

	public static final String _serializedATN =
		"\3\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd\2\34\u0091\b\1\4\2"+
		"\t\2\4\3\t\3\4\4\t\4\4\5\t\5\4\6\t\6\4\7\t\7\4\b\t\b\4\t\t\t\4\n\t\n\4"+
		"\13\t\13\4\f\t\f\4\r\t\r\4\16\t\16\4\17\t\17\4\20\t\20\4\21\t\21\4\22"+
		"\t\22\4\23\t\23\4\24\t\24\4\25\t\25\4\26\t\26\4\27\t\27\4\30\t\30\4\31"+
		"\t\31\4\32\t\32\4\33\t\33\3\2\3\2\3\2\3\2\3\2\3\2\3\2\3\2\3\2\5\2A\n\2"+
		"\3\3\3\3\7\3E\n\3\f\3\16\3H\13\3\3\3\3\3\3\4\3\4\3\5\3\5\3\6\3\6\3\7\3"+
		"\7\3\b\3\b\3\t\3\t\3\n\3\n\3\13\3\13\3\f\3\f\3\r\3\r\3\r\3\16\3\16\3\16"+
		"\3\17\3\17\3\20\3\20\3\21\3\21\3\21\3\22\3\22\3\22\3\23\3\23\3\23\3\23"+
		"\3\24\3\24\3\24\3\24\3\24\3\24\3\24\3\25\3\25\3\26\3\26\3\27\6\27~\n\27"+
		"\r\27\16\27\177\3\30\6\30\u0083\n\30\r\30\16\30\u0084\3\31\3\31\3\32\3"+
		"\32\3\33\6\33\u008c\n\33\r\33\16\33\u008d\3\33\3\33\2\2\34\3\3\5\4\7\5"+
		"\t\6\13\7\r\b\17\t\21\n\23\13\25\f\27\r\31\16\33\17\35\20\37\21!\22#\23"+
		"%\24\'\25)\26+\27-\30/\31\61\32\63\33\65\34\3\2\6\3\2$$\3\2\62;\5\2\62"+
		";C\\c|\5\2\13\f\17\17\"\"\u0095\2\3\3\2\2\2\2\5\3\2\2\2\2\7\3\2\2\2\2"+
		"\t\3\2\2\2\2\13\3\2\2\2\2\r\3\2\2\2\2\17\3\2\2\2\2\21\3\2\2\2\2\23\3\2"+
		"\2\2\2\25\3\2\2\2\2\27\3\2\2\2\2\31\3\2\2\2\2\33\3\2\2\2\2\35\3\2\2\2"+
		"\2\37\3\2\2\2\2!\3\2\2\2\2#\3\2\2\2\2%\3\2\2\2\2\'\3\2\2\2\2)\3\2\2\2"+
		"\2+\3\2\2\2\2-\3\2\2\2\2/\3\2\2\2\2\61\3\2\2\2\2\63\3\2\2\2\2\65\3\2\2"+
		"\2\3@\3\2\2\2\5B\3\2\2\2\7K\3\2\2\2\tM\3\2\2\2\13O\3\2\2\2\rQ\3\2\2\2"+
		"\17S\3\2\2\2\21U\3\2\2\2\23W\3\2\2\2\25Y\3\2\2\2\27[\3\2\2\2\31]\3\2\2"+
		"\2\33`\3\2\2\2\35c\3\2\2\2\37e\3\2\2\2!g\3\2\2\2#j\3\2\2\2%m\3\2\2\2\'"+
		"q\3\2\2\2)x\3\2\2\2+z\3\2\2\2-}\3\2\2\2/\u0082\3\2\2\2\61\u0086\3\2\2"+
		"\2\63\u0088\3\2\2\2\65\u008b\3\2\2\2\678\7k\2\289\7p\2\29A\7v\2\2:;\7"+
		"u\2\2;<\7v\2\2<=\7t\2\2=>\7k\2\2>?\7p\2\2?A\7i\2\2@\67\3\2\2\2@:\3\2\2"+
		"\2A\4\3\2\2\2BF\7$\2\2CE\n\2\2\2DC\3\2\2\2EH\3\2\2\2FD\3\2\2\2FG\3\2\2"+
		"\2GI\3\2\2\2HF\3\2\2\2IJ\7$\2\2J\6\3\2\2\2KL\7*\2\2L\b\3\2\2\2MN\7+\2"+
		"\2N\n\3\2\2\2OP\7$\2\2P\f\3\2\2\2QR\7}\2\2R\16\3\2\2\2ST\7\177\2\2T\20"+
		"\3\2\2\2UV\7-\2\2V\22\3\2\2\2WX\7/\2\2X\24\3\2\2\2YZ\7\61\2\2Z\26\3\2"+
		"\2\2[\\\7,\2\2\\\30\3\2\2\2]^\7>\2\2^_\7?\2\2_\32\3\2\2\2`a\7@\2\2ab\7"+
		"?\2\2b\34\3\2\2\2cd\7>\2\2d\36\3\2\2\2ef\7@\2\2f \3\2\2\2gh\7?\2\2hi\7"+
		"@\2\2i\"\3\2\2\2jk\7k\2\2kl\7h\2\2l$\3\2\2\2mn\7x\2\2no\7c\2\2op\7t\2"+
		"\2p&\3\2\2\2qr\7t\2\2rs\7g\2\2st\7v\2\2tu\7w\2\2uv\7t\2\2vw\7p\2\2w(\3"+
		"\2\2\2xy\7?\2\2y*\3\2\2\2z{\7.\2\2{,\3\2\2\2|~\t\3\2\2}|\3\2\2\2~\177"+
		"\3\2\2\2\177}\3\2\2\2\177\u0080\3\2\2\2\u0080.\3\2\2\2\u0081\u0083\t\4"+
		"\2\2\u0082\u0081\3\2\2\2\u0083\u0084\3\2\2\2\u0084\u0082\3\2\2\2\u0084"+
		"\u0085\3\2\2\2\u0085\60\3\2\2\2\u0086\u0087\7=\2\2\u0087\62\3\2\2\2\u0088"+
		"\u0089\7<\2\2\u0089\64\3\2\2\2\u008a\u008c\t\5\2\2\u008b\u008a\3\2\2\2"+
		"\u008c\u008d\3\2\2\2\u008d\u008b\3\2\2\2\u008d\u008e\3\2\2\2\u008e\u008f"+
		"\3\2\2\2\u008f\u0090\b\33\2\2\u0090\66\3\2\2\2\b\2@F\177\u0084\u008d\3"+
		"\b\2\2";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}