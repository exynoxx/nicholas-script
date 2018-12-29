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
		PLUS=8, MINUS=9, DIV=10, MULT=11, LE=12, GE=13, LT=14, GT=15, TILDE=16, 
		ARROW=17, IF=18, ELSE=19, WHILE=20, LOOP=21, VAR=22, RETURN=23, EQ=24, 
		EQUAL=25, COMMA=26, NUM=27, ID=28, SEMICOLON=29, COLON=30, COMMENT=31, 
		LINE_COMMENT=32, WS=33;
	public static String[] modeNames = {
		"DEFAULT_MODE"
	};

	public static final String[] ruleNames = {
		"TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", "RBRACKET", 
		"PLUS", "MINUS", "DIV", "MULT", "LE", "GE", "LT", "GT", "TILDE", "ARROW", 
		"IF", "ELSE", "WHILE", "LOOP", "VAR", "RETURN", "EQ", "EQUAL", "COMMA", 
		"NUM", "ID", "SEMICOLON", "COLON", "COMMENT", "LINE_COMMENT", "WS"
	};

	private static final String[] _LITERAL_NAMES = {
		null, null, null, "'('", "')'", "'\"'", "'{'", "'}'", "'+'", "'-'", "'/'", 
		"'*'", "'<='", "'>='", "'<'", "'>'", "'~'", "'=>'", "'if'", "'else'", 
		"'while'", "'loop'", "'var'", "'return'", "'='", "'=='", "','", null, 
		null, "';'", "':'"
	};
	private static final String[] _SYMBOLIC_NAMES = {
		null, "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", "RBRACKET", 
		"PLUS", "MINUS", "DIV", "MULT", "LE", "GE", "LT", "GT", "TILDE", "ARROW", 
		"IF", "ELSE", "WHILE", "LOOP", "VAR", "RETURN", "EQ", "EQUAL", "COMMA", 
		"NUM", "ID", "SEMICOLON", "COLON", "COMMENT", "LINE_COMMENT", "WS"
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
		"\3\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd\2#\u00cb\b\1\4\2\t"+
		"\2\4\3\t\3\4\4\t\4\4\5\t\5\4\6\t\6\4\7\t\7\4\b\t\b\4\t\t\t\4\n\t\n\4\13"+
		"\t\13\4\f\t\f\4\r\t\r\4\16\t\16\4\17\t\17\4\20\t\20\4\21\t\21\4\22\t\22"+
		"\4\23\t\23\4\24\t\24\4\25\t\25\4\26\t\26\4\27\t\27\4\30\t\30\4\31\t\31"+
		"\4\32\t\32\4\33\t\33\4\34\t\34\4\35\t\35\4\36\t\36\4\37\t\37\4 \t \4!"+
		"\t!\4\"\t\"\3\2\3\2\3\2\3\2\3\2\3\2\3\2\3\2\3\2\5\2O\n\2\3\3\3\3\7\3S"+
		"\n\3\f\3\16\3V\13\3\3\3\3\3\3\4\3\4\3\5\3\5\3\6\3\6\3\7\3\7\3\b\3\b\3"+
		"\t\3\t\3\n\3\n\3\13\3\13\3\f\3\f\3\r\3\r\3\r\3\16\3\16\3\16\3\17\3\17"+
		"\3\20\3\20\3\21\3\21\3\22\3\22\3\22\3\23\3\23\3\23\3\24\3\24\3\24\3\24"+
		"\3\24\3\25\3\25\3\25\3\25\3\25\3\25\3\26\3\26\3\26\3\26\3\26\3\27\3\27"+
		"\3\27\3\27\3\30\3\30\3\30\3\30\3\30\3\30\3\30\3\31\3\31\3\32\3\32\3\32"+
		"\3\33\3\33\3\34\6\34\u00a1\n\34\r\34\16\34\u00a2\3\35\6\35\u00a6\n\35"+
		"\r\35\16\35\u00a7\3\36\3\36\3\37\3\37\3 \3 \3 \3 \7 \u00b2\n \f \16 \u00b5"+
		"\13 \3 \3 \3 \3 \3 \3!\3!\7!\u00be\n!\f!\16!\u00c1\13!\3!\3!\3\"\6\"\u00c6"+
		"\n\"\r\"\16\"\u00c7\3\"\3\"\3\u00b3\2#\3\3\5\4\7\5\t\6\13\7\r\b\17\t\21"+
		"\n\23\13\25\f\27\r\31\16\33\17\35\20\37\21!\22#\23%\24\'\25)\26+\27-\30"+
		"/\31\61\32\63\33\65\34\67\359\36;\37= ?!A\"C#\3\2\7\3\2$$\3\2\62;\5\2"+
		"\62;C\\c|\4\2\f\f\17\17\5\2\13\f\17\17\"\"\u00d1\2\3\3\2\2\2\2\5\3\2\2"+
		"\2\2\7\3\2\2\2\2\t\3\2\2\2\2\13\3\2\2\2\2\r\3\2\2\2\2\17\3\2\2\2\2\21"+
		"\3\2\2\2\2\23\3\2\2\2\2\25\3\2\2\2\2\27\3\2\2\2\2\31\3\2\2\2\2\33\3\2"+
		"\2\2\2\35\3\2\2\2\2\37\3\2\2\2\2!\3\2\2\2\2#\3\2\2\2\2%\3\2\2\2\2\'\3"+
		"\2\2\2\2)\3\2\2\2\2+\3\2\2\2\2-\3\2\2\2\2/\3\2\2\2\2\61\3\2\2\2\2\63\3"+
		"\2\2\2\2\65\3\2\2\2\2\67\3\2\2\2\29\3\2\2\2\2;\3\2\2\2\2=\3\2\2\2\2?\3"+
		"\2\2\2\2A\3\2\2\2\2C\3\2\2\2\3N\3\2\2\2\5P\3\2\2\2\7Y\3\2\2\2\t[\3\2\2"+
		"\2\13]\3\2\2\2\r_\3\2\2\2\17a\3\2\2\2\21c\3\2\2\2\23e\3\2\2\2\25g\3\2"+
		"\2\2\27i\3\2\2\2\31k\3\2\2\2\33n\3\2\2\2\35q\3\2\2\2\37s\3\2\2\2!u\3\2"+
		"\2\2#w\3\2\2\2%z\3\2\2\2\'}\3\2\2\2)\u0082\3\2\2\2+\u0088\3\2\2\2-\u008d"+
		"\3\2\2\2/\u0091\3\2\2\2\61\u0098\3\2\2\2\63\u009a\3\2\2\2\65\u009d\3\2"+
		"\2\2\67\u00a0\3\2\2\29\u00a5\3\2\2\2;\u00a9\3\2\2\2=\u00ab\3\2\2\2?\u00ad"+
		"\3\2\2\2A\u00bb\3\2\2\2C\u00c5\3\2\2\2EF\7k\2\2FG\7p\2\2GO\7v\2\2HI\7"+
		"u\2\2IJ\7v\2\2JK\7t\2\2KL\7k\2\2LM\7p\2\2MO\7i\2\2NE\3\2\2\2NH\3\2\2\2"+
		"O\4\3\2\2\2PT\7$\2\2QS\n\2\2\2RQ\3\2\2\2SV\3\2\2\2TR\3\2\2\2TU\3\2\2\2"+
		"UW\3\2\2\2VT\3\2\2\2WX\7$\2\2X\6\3\2\2\2YZ\7*\2\2Z\b\3\2\2\2[\\\7+\2\2"+
		"\\\n\3\2\2\2]^\7$\2\2^\f\3\2\2\2_`\7}\2\2`\16\3\2\2\2ab\7\177\2\2b\20"+
		"\3\2\2\2cd\7-\2\2d\22\3\2\2\2ef\7/\2\2f\24\3\2\2\2gh\7\61\2\2h\26\3\2"+
		"\2\2ij\7,\2\2j\30\3\2\2\2kl\7>\2\2lm\7?\2\2m\32\3\2\2\2no\7@\2\2op\7?"+
		"\2\2p\34\3\2\2\2qr\7>\2\2r\36\3\2\2\2st\7@\2\2t \3\2\2\2uv\7\u0080\2\2"+
		"v\"\3\2\2\2wx\7?\2\2xy\7@\2\2y$\3\2\2\2z{\7k\2\2{|\7h\2\2|&\3\2\2\2}~"+
		"\7g\2\2~\177\7n\2\2\177\u0080\7u\2\2\u0080\u0081\7g\2\2\u0081(\3\2\2\2"+
		"\u0082\u0083\7y\2\2\u0083\u0084\7j\2\2\u0084\u0085\7k\2\2\u0085\u0086"+
		"\7n\2\2\u0086\u0087\7g\2\2\u0087*\3\2\2\2\u0088\u0089\7n\2\2\u0089\u008a"+
		"\7q\2\2\u008a\u008b\7q\2\2\u008b\u008c\7r\2\2\u008c,\3\2\2\2\u008d\u008e"+
		"\7x\2\2\u008e\u008f\7c\2\2\u008f\u0090\7t\2\2\u0090.\3\2\2\2\u0091\u0092"+
		"\7t\2\2\u0092\u0093\7g\2\2\u0093\u0094\7v\2\2\u0094\u0095\7w\2\2\u0095"+
		"\u0096\7t\2\2\u0096\u0097\7p\2\2\u0097\60\3\2\2\2\u0098\u0099\7?\2\2\u0099"+
		"\62\3\2\2\2\u009a\u009b\7?\2\2\u009b\u009c\7?\2\2\u009c\64\3\2\2\2\u009d"+
		"\u009e\7.\2\2\u009e\66\3\2\2\2\u009f\u00a1\t\3\2\2\u00a0\u009f\3\2\2\2"+
		"\u00a1\u00a2\3\2\2\2\u00a2\u00a0\3\2\2\2\u00a2\u00a3\3\2\2\2\u00a38\3"+
		"\2\2\2\u00a4\u00a6\t\4\2\2\u00a5\u00a4\3\2\2\2\u00a6\u00a7\3\2\2\2\u00a7"+
		"\u00a5\3\2\2\2\u00a7\u00a8\3\2\2\2\u00a8:\3\2\2\2\u00a9\u00aa\7=\2\2\u00aa"+
		"<\3\2\2\2\u00ab\u00ac\7<\2\2\u00ac>\3\2\2\2\u00ad\u00ae\7\61\2\2\u00ae"+
		"\u00af\7,\2\2\u00af\u00b3\3\2\2\2\u00b0\u00b2\13\2\2\2\u00b1\u00b0\3\2"+
		"\2\2\u00b2\u00b5\3\2\2\2\u00b3\u00b4\3\2\2\2\u00b3\u00b1\3\2\2\2\u00b4"+
		"\u00b6\3\2\2\2\u00b5\u00b3\3\2\2\2\u00b6\u00b7\7,\2\2\u00b7\u00b8\7\61"+
		"\2\2\u00b8\u00b9\3\2\2\2\u00b9\u00ba\b \2\2\u00ba@\3\2\2\2\u00bb\u00bf"+
		"\7%\2\2\u00bc\u00be\n\5\2\2\u00bd\u00bc\3\2\2\2\u00be\u00c1\3\2\2\2\u00bf"+
		"\u00bd\3\2\2\2\u00bf\u00c0\3\2\2\2\u00c0\u00c2\3\2\2\2\u00c1\u00bf\3\2"+
		"\2\2\u00c2\u00c3\b!\2\2\u00c3B\3\2\2\2\u00c4\u00c6\t\6\2\2\u00c5\u00c4"+
		"\3\2\2\2\u00c6\u00c7\3\2\2\2\u00c7\u00c5\3\2\2\2\u00c7\u00c8\3\2\2\2\u00c8"+
		"\u00c9\3\2\2\2\u00c9\u00ca\b\"\2\2\u00caD\3\2\2\2\n\2NT\u00a2\u00a7\u00b3"+
		"\u00bf\u00c7\3\b\2\2";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}