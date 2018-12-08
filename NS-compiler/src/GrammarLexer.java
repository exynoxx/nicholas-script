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
		SIMPLEVAL=1, TYPE=2, STRING=3, LPAREN=4, RPAREN=5, QUOTE=6, LBRACKET=7, 
		RBRACKET=8, LSQUARE=9, RSQUARE=10, PLUS=11, MINUS=12, DIV=13, MULT=14, 
		LE=15, GE=16, LT=17, GT=18, DOT=19, TILDE=20, ARROW=21, IF=22, ELSE=23, 
		VAR=24, RETURN=25, EQ=26, COMMA=27, NUM=28, ID=29, SEMICOLON=30, COLON=31, 
		WS=32;
	public static String[] modeNames = {
		"DEFAULT_MODE"
	};

	public static final String[] ruleNames = {
		"SIMPLEVAL", "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", 
		"RBRACKET", "LSQUARE", "RSQUARE", "PLUS", "MINUS", "DIV", "MULT", "LE", 
		"GE", "LT", "GT", "DOT", "TILDE", "ARROW", "IF", "ELSE", "VAR", "RETURN", 
		"EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", "WS"
	};

	private static final String[] _LITERAL_NAMES = {
		null, null, null, null, "'('", "')'", "'\"'", "'{'", "'}'", "'['", "']'", 
		"'+'", "'-'", "'/'", "'*'", "'<='", "'>='", "'<'", "'>'", "'.'", "'~'", 
		"'=>'", "'if'", "'else'", "'var'", "'return'", "'='", "','", null, null, 
		"';'", "':'"
	};
	private static final String[] _SYMBOLIC_NAMES = {
		null, "SIMPLEVAL", "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", 
		"RBRACKET", "LSQUARE", "RSQUARE", "PLUS", "MINUS", "DIV", "MULT", "LE", 
		"GE", "LT", "GT", "DOT", "TILDE", "ARROW", "IF", "ELSE", "VAR", "RETURN", 
		"EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", "WS"
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
		"\3\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd\2\"\u00af\b\1\4\2\t"+
		"\2\4\3\t\3\4\4\t\4\4\5\t\5\4\6\t\6\4\7\t\7\4\b\t\b\4\t\t\t\4\n\t\n\4\13"+
		"\t\13\4\f\t\f\4\r\t\r\4\16\t\16\4\17\t\17\4\20\t\20\4\21\t\21\4\22\t\22"+
		"\4\23\t\23\4\24\t\24\4\25\t\25\4\26\t\26\4\27\t\27\4\30\t\30\4\31\t\31"+
		"\4\32\t\32\4\33\t\33\4\34\t\34\4\35\t\35\4\36\t\36\4\37\t\37\4 \t \4!"+
		"\t!\3\2\3\2\3\2\5\2G\n\2\3\3\3\3\3\3\3\3\3\3\3\3\3\3\3\3\3\3\5\3R\n\3"+
		"\3\4\3\4\7\4V\n\4\f\4\16\4Y\13\4\3\4\3\4\3\5\3\5\3\6\3\6\3\7\3\7\3\b\3"+
		"\b\3\t\3\t\3\n\3\n\3\13\3\13\3\f\3\f\3\r\3\r\3\16\3\16\3\17\3\17\3\20"+
		"\3\20\3\20\3\21\3\21\3\21\3\22\3\22\3\23\3\23\3\24\3\24\3\25\3\25\3\26"+
		"\3\26\3\26\3\27\3\27\3\27\3\30\3\30\3\30\3\30\3\30\3\31\3\31\3\31\3\31"+
		"\3\32\3\32\3\32\3\32\3\32\3\32\3\32\3\33\3\33\3\34\3\34\3\35\6\35\u009c"+
		"\n\35\r\35\16\35\u009d\3\36\6\36\u00a1\n\36\r\36\16\36\u00a2\3\37\3\37"+
		"\3 \3 \3!\6!\u00aa\n!\r!\16!\u00ab\3!\3!\2\2\"\3\3\5\4\7\5\t\6\13\7\r"+
		"\b\17\t\21\n\23\13\25\f\27\r\31\16\33\17\35\20\37\21!\22#\23%\24\'\25"+
		")\26+\27-\30/\31\61\32\63\33\65\34\67\359\36;\37= ?!A\"\3\2\6\3\2$$\3"+
		"\2\62;\5\2\62;C\\c|\5\2\13\f\17\17\"\"\u00b5\2\3\3\2\2\2\2\5\3\2\2\2\2"+
		"\7\3\2\2\2\2\t\3\2\2\2\2\13\3\2\2\2\2\r\3\2\2\2\2\17\3\2\2\2\2\21\3\2"+
		"\2\2\2\23\3\2\2\2\2\25\3\2\2\2\2\27\3\2\2\2\2\31\3\2\2\2\2\33\3\2\2\2"+
		"\2\35\3\2\2\2\2\37\3\2\2\2\2!\3\2\2\2\2#\3\2\2\2\2%\3\2\2\2\2\'\3\2\2"+
		"\2\2)\3\2\2\2\2+\3\2\2\2\2-\3\2\2\2\2/\3\2\2\2\2\61\3\2\2\2\2\63\3\2\2"+
		"\2\2\65\3\2\2\2\2\67\3\2\2\2\29\3\2\2\2\2;\3\2\2\2\2=\3\2\2\2\2?\3\2\2"+
		"\2\2A\3\2\2\2\3F\3\2\2\2\5Q\3\2\2\2\7S\3\2\2\2\t\\\3\2\2\2\13^\3\2\2\2"+
		"\r`\3\2\2\2\17b\3\2\2\2\21d\3\2\2\2\23f\3\2\2\2\25h\3\2\2\2\27j\3\2\2"+
		"\2\31l\3\2\2\2\33n\3\2\2\2\35p\3\2\2\2\37r\3\2\2\2!u\3\2\2\2#x\3\2\2\2"+
		"%z\3\2\2\2\'|\3\2\2\2)~\3\2\2\2+\u0080\3\2\2\2-\u0083\3\2\2\2/\u0086\3"+
		"\2\2\2\61\u008b\3\2\2\2\63\u008f\3\2\2\2\65\u0096\3\2\2\2\67\u0098\3\2"+
		"\2\29\u009b\3\2\2\2;\u00a0\3\2\2\2=\u00a4\3\2\2\2?\u00a6\3\2\2\2A\u00a9"+
		"\3\2\2\2CG\5;\36\2DG\59\35\2EG\5\7\4\2FC\3\2\2\2FD\3\2\2\2FE\3\2\2\2G"+
		"\4\3\2\2\2HI\7k\2\2IJ\7p\2\2JR\7v\2\2KL\7u\2\2LM\7v\2\2MN\7t\2\2NO\7k"+
		"\2\2OP\7p\2\2PR\7i\2\2QH\3\2\2\2QK\3\2\2\2R\6\3\2\2\2SW\7$\2\2TV\n\2\2"+
		"\2UT\3\2\2\2VY\3\2\2\2WU\3\2\2\2WX\3\2\2\2XZ\3\2\2\2YW\3\2\2\2Z[\7$\2"+
		"\2[\b\3\2\2\2\\]\7*\2\2]\n\3\2\2\2^_\7+\2\2_\f\3\2\2\2`a\7$\2\2a\16\3"+
		"\2\2\2bc\7}\2\2c\20\3\2\2\2de\7\177\2\2e\22\3\2\2\2fg\7]\2\2g\24\3\2\2"+
		"\2hi\7_\2\2i\26\3\2\2\2jk\7-\2\2k\30\3\2\2\2lm\7/\2\2m\32\3\2\2\2no\7"+
		"\61\2\2o\34\3\2\2\2pq\7,\2\2q\36\3\2\2\2rs\7>\2\2st\7?\2\2t \3\2\2\2u"+
		"v\7@\2\2vw\7?\2\2w\"\3\2\2\2xy\7>\2\2y$\3\2\2\2z{\7@\2\2{&\3\2\2\2|}\7"+
		"\60\2\2}(\3\2\2\2~\177\7\u0080\2\2\177*\3\2\2\2\u0080\u0081\7?\2\2\u0081"+
		"\u0082\7@\2\2\u0082,\3\2\2\2\u0083\u0084\7k\2\2\u0084\u0085\7h\2\2\u0085"+
		".\3\2\2\2\u0086\u0087\7g\2\2\u0087\u0088\7n\2\2\u0088\u0089\7u\2\2\u0089"+
		"\u008a\7g\2\2\u008a\60\3\2\2\2\u008b\u008c\7x\2\2\u008c\u008d\7c\2\2\u008d"+
		"\u008e\7t\2\2\u008e\62\3\2\2\2\u008f\u0090\7t\2\2\u0090\u0091\7g\2\2\u0091"+
		"\u0092\7v\2\2\u0092\u0093\7w\2\2\u0093\u0094\7t\2\2\u0094\u0095\7p\2\2"+
		"\u0095\64\3\2\2\2\u0096\u0097\7?\2\2\u0097\66\3\2\2\2\u0098\u0099\7.\2"+
		"\2\u00998\3\2\2\2\u009a\u009c\t\3\2\2\u009b\u009a\3\2\2\2\u009c\u009d"+
		"\3\2\2\2\u009d\u009b\3\2\2\2\u009d\u009e\3\2\2\2\u009e:\3\2\2\2\u009f"+
		"\u00a1\t\4\2\2\u00a0\u009f\3\2\2\2\u00a1\u00a2\3\2\2\2\u00a2\u00a0\3\2"+
		"\2\2\u00a2\u00a3\3\2\2\2\u00a3<\3\2\2\2\u00a4\u00a5\7=\2\2\u00a5>\3\2"+
		"\2\2\u00a6\u00a7\7<\2\2\u00a7@\3\2\2\2\u00a8\u00aa\t\5\2\2\u00a9\u00a8"+
		"\3\2\2\2\u00aa\u00ab\3\2\2\2\u00ab\u00a9\3\2\2\2\u00ab\u00ac\3\2\2\2\u00ac"+
		"\u00ad\3\2\2\2\u00ad\u00ae\b!\2\2\u00aeB\3\2\2\2\t\2FQW\u009d\u00a2\u00ab"+
		"\3\b\2\2";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}