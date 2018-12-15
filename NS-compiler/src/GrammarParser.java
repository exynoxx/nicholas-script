// Generated from Grammar.g4 by ANTLR 4.5.3
import org.antlr.v4.runtime.atn.*;
import org.antlr.v4.runtime.dfa.DFA;
import org.antlr.v4.runtime.*;
import org.antlr.v4.runtime.misc.*;
import org.antlr.v4.runtime.tree.*;
import java.util.List;
import java.util.Iterator;
import java.util.ArrayList;

@SuppressWarnings({"all", "warnings", "unchecked", "unused", "cast"})
public class GrammarParser extends Parser {
	static { RuntimeMetaData.checkVersion("4.5.3", RuntimeMetaData.VERSION); }

	protected static final DFA[] _decisionToDFA;
	protected static final PredictionContextCache _sharedContextCache =
		new PredictionContextCache();
	public static final int
		TYPE=1, STRING=2, LPAREN=3, RPAREN=4, QUOTE=5, LBRACKET=6, RBRACKET=7, 
		PLUS=8, MINUS=9, DIV=10, MULT=11, LE=12, GE=13, LT=14, GT=15, TILDE=16, 
		ARROW=17, IF=18, VAR=19, RETURN=20, EQ=21, COMMA=22, NUM=23, ID=24, SEMICOLON=25, 
		COLON=26, WS=27;
	public static final int
		RULE_start = 0, RULE_program = 1, RULE_statement = 2, RULE_assign = 3, 
		RULE_iff = 4, RULE_returnn = 5, RULE_call = 6, RULE_callarg = 7, RULE_eval = 8, 
		RULE_binop = 9, RULE_sign = 10, RULE_function = 11, RULE_fbody = 12, RULE_arg = 13, 
		RULE_block = 14, RULE_value = 15;
	public static final String[] ruleNames = {
		"start", "program", "statement", "assign", "iff", "returnn", "call", "callarg", 
		"eval", "binop", "sign", "function", "fbody", "arg", "block", "value"
	};

	private static final String[] _LITERAL_NAMES = {
		null, null, null, "'('", "')'", "'\"'", "'{'", "'}'", "'+'", "'-'", "'/'", 
		"'*'", "'<='", "'>='", "'<'", "'>'", "'~'", "'=>'", "'if'", "'var'", "'return'", 
		"'='", "','", null, null, "';'", "':'"
	};
	private static final String[] _SYMBOLIC_NAMES = {
		null, "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", "LBRACKET", "RBRACKET", 
		"PLUS", "MINUS", "DIV", "MULT", "LE", "GE", "LT", "GT", "TILDE", "ARROW", 
		"IF", "VAR", "RETURN", "EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", 
		"WS"
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

	@Override
	public String getGrammarFileName() { return "Grammar.g4"; }

	@Override
	public String[] getRuleNames() { return ruleNames; }

	@Override
	public String getSerializedATN() { return _serializedATN; }

	@Override
	public ATN getATN() { return _ATN; }

	public GrammarParser(TokenStream input) {
		super(input);
		_interp = new ParserATNSimulator(this,_ATN,_decisionToDFA,_sharedContextCache);
	}
	public static class StartContext extends ParserRuleContext {
		public ProgramContext program() {
			return getRuleContext(ProgramContext.class,0);
		}
		public TerminalNode EOF() { return getToken(GrammarParser.EOF, 0); }
		public StartContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_start; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterStart(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitStart(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitStart(this);
			else return visitor.visitChildren(this);
		}
	}

	public final StartContext start() throws RecognitionException {
		StartContext _localctx = new StartContext(_ctx, getState());
		enterRule(_localctx, 0, RULE_start);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(32);
			program();
			setState(33);
			match(EOF);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class ProgramContext extends ParserRuleContext {
		public List<StatementContext> statement() {
			return getRuleContexts(StatementContext.class);
		}
		public StatementContext statement(int i) {
			return getRuleContext(StatementContext.class,i);
		}
		public List<TerminalNode> SEMICOLON() { return getTokens(GrammarParser.SEMICOLON); }
		public TerminalNode SEMICOLON(int i) {
			return getToken(GrammarParser.SEMICOLON, i);
		}
		public ProgramContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_program; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterProgram(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitProgram(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitProgram(this);
			else return visitor.visitChildren(this);
		}
	}

	public final ProgramContext program() throws RecognitionException {
		ProgramContext _localctx = new ProgramContext(_ctx, getState());
		enterRule(_localctx, 2, RULE_program);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(38); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(35);
				statement();
				setState(36);
				match(SEMICOLON);
				}
				}
				setState(40); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( (((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << IF) | (1L << VAR) | (1L << RETURN) | (1L << ID))) != 0) );
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class StatementContext extends ParserRuleContext {
		public AssignContext assign() {
			return getRuleContext(AssignContext.class,0);
		}
		public IffContext iff() {
			return getRuleContext(IffContext.class,0);
		}
		public ReturnnContext returnn() {
			return getRuleContext(ReturnnContext.class,0);
		}
		public CallContext call() {
			return getRuleContext(CallContext.class,0);
		}
		public StatementContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_statement; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterStatement(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitStatement(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitStatement(this);
			else return visitor.visitChildren(this);
		}
	}

	public final StatementContext statement() throws RecognitionException {
		StatementContext _localctx = new StatementContext(_ctx, getState());
		enterRule(_localctx, 4, RULE_statement);
		try {
			setState(46);
			switch (_input.LA(1)) {
			case VAR:
				enterOuterAlt(_localctx, 1);
				{
				setState(42);
				assign();
				}
				break;
			case IF:
				enterOuterAlt(_localctx, 2);
				{
				setState(43);
				iff();
				}
				break;
			case RETURN:
				enterOuterAlt(_localctx, 3);
				{
				setState(44);
				returnn();
				}
				break;
			case ID:
				enterOuterAlt(_localctx, 4);
				{
				setState(45);
				call();
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class AssignContext extends ParserRuleContext {
		public AssignContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_assign; }
	 
		public AssignContext() { }
		public void copyFrom(AssignContext ctx) {
			super.copyFrom(ctx);
		}
	}
	public static class AssignevalContext extends AssignContext {
		public TerminalNode VAR() { return getToken(GrammarParser.VAR, 0); }
		public TerminalNode ID() { return getToken(GrammarParser.ID, 0); }
		public TerminalNode EQ() { return getToken(GrammarParser.EQ, 0); }
		public EvalContext eval() {
			return getRuleContext(EvalContext.class,0);
		}
		public TerminalNode COLON() { return getToken(GrammarParser.COLON, 0); }
		public TerminalNode TYPE() { return getToken(GrammarParser.TYPE, 0); }
		public AssignevalContext(AssignContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterAssigneval(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitAssigneval(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitAssigneval(this);
			else return visitor.visitChildren(this);
		}
	}
	public static class AssignfunctionContext extends AssignContext {
		public TerminalNode VAR() { return getToken(GrammarParser.VAR, 0); }
		public TerminalNode ID() { return getToken(GrammarParser.ID, 0); }
		public TerminalNode EQ() { return getToken(GrammarParser.EQ, 0); }
		public FunctionContext function() {
			return getRuleContext(FunctionContext.class,0);
		}
		public TerminalNode COLON() { return getToken(GrammarParser.COLON, 0); }
		public TerminalNode TYPE() { return getToken(GrammarParser.TYPE, 0); }
		public AssignfunctionContext(AssignContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterAssignfunction(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitAssignfunction(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitAssignfunction(this);
			else return visitor.visitChildren(this);
		}
	}

	public final AssignContext assign() throws RecognitionException {
		AssignContext _localctx = new AssignContext(_ctx, getState());
		enterRule(_localctx, 6, RULE_assign);
		int _la;
		try {
			setState(64);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,4,_ctx) ) {
			case 1:
				_localctx = new AssignevalContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(48);
				match(VAR);
				setState(49);
				match(ID);
				setState(52);
				_la = _input.LA(1);
				if (_la==COLON) {
					{
					setState(50);
					match(COLON);
					setState(51);
					match(TYPE);
					}
				}

				setState(54);
				match(EQ);
				setState(55);
				eval();
				}
				break;
			case 2:
				_localctx = new AssignfunctionContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(56);
				match(VAR);
				setState(57);
				match(ID);
				setState(60);
				_la = _input.LA(1);
				if (_la==COLON) {
					{
					setState(58);
					match(COLON);
					setState(59);
					match(TYPE);
					}
				}

				setState(62);
				match(EQ);
				setState(63);
				function();
				}
				break;
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class IffContext extends ParserRuleContext {
		public TerminalNode IF() { return getToken(GrammarParser.IF, 0); }
		public TerminalNode LPAREN() { return getToken(GrammarParser.LPAREN, 0); }
		public BinopContext binop() {
			return getRuleContext(BinopContext.class,0);
		}
		public TerminalNode RPAREN() { return getToken(GrammarParser.RPAREN, 0); }
		public BlockContext block() {
			return getRuleContext(BlockContext.class,0);
		}
		public IffContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_iff; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterIff(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitIff(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitIff(this);
			else return visitor.visitChildren(this);
		}
	}

	public final IffContext iff() throws RecognitionException {
		IffContext _localctx = new IffContext(_ctx, getState());
		enterRule(_localctx, 8, RULE_iff);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(66);
			match(IF);
			setState(67);
			match(LPAREN);
			setState(68);
			binop();
			setState(69);
			match(RPAREN);
			setState(70);
			block();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class ReturnnContext extends ParserRuleContext {
		public TerminalNode RETURN() { return getToken(GrammarParser.RETURN, 0); }
		public EvalContext eval() {
			return getRuleContext(EvalContext.class,0);
		}
		public ReturnnContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_returnn; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterReturnn(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitReturnn(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitReturnn(this);
			else return visitor.visitChildren(this);
		}
	}

	public final ReturnnContext returnn() throws RecognitionException {
		ReturnnContext _localctx = new ReturnnContext(_ctx, getState());
		enterRule(_localctx, 10, RULE_returnn);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(72);
			match(RETURN);
			setState(73);
			eval();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class CallContext extends ParserRuleContext {
		public TerminalNode ID() { return getToken(GrammarParser.ID, 0); }
		public TerminalNode COLON() { return getToken(GrammarParser.COLON, 0); }
		public List<CallargContext> callarg() {
			return getRuleContexts(CallargContext.class);
		}
		public CallargContext callarg(int i) {
			return getRuleContext(CallargContext.class,i);
		}
		public CallContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_call; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterCall(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitCall(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitCall(this);
			else return visitor.visitChildren(this);
		}
	}

	public final CallContext call() throws RecognitionException {
		CallContext _localctx = new CallContext(_ctx, getState());
		enterRule(_localctx, 12, RULE_call);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(75);
			match(ID);
			setState(76);
			match(COLON);
			setState(80);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while ((((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << STRING) | (1L << LPAREN) | (1L << NUM) | (1L << ID))) != 0)) {
				{
				{
				setState(77);
				callarg();
				}
				}
				setState(82);
				_errHandler.sync(this);
				_la = _input.LA(1);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class CallargContext extends ParserRuleContext {
		public CallargContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_callarg; }
	 
		public CallargContext() { }
		public void copyFrom(CallargContext ctx) {
			super.copyFrom(ctx);
		}
	}
	public static class CallargvalueContext extends CallargContext {
		public ValueContext value() {
			return getRuleContext(ValueContext.class,0);
		}
		public CallargvalueContext(CallargContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterCallargvalue(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitCallargvalue(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitCallargvalue(this);
			else return visitor.visitChildren(this);
		}
	}
	public static class CallargevalContext extends CallargContext {
		public TerminalNode LPAREN() { return getToken(GrammarParser.LPAREN, 0); }
		public EvalContext eval() {
			return getRuleContext(EvalContext.class,0);
		}
		public TerminalNode RPAREN() { return getToken(GrammarParser.RPAREN, 0); }
		public CallargevalContext(CallargContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterCallargeval(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitCallargeval(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitCallargeval(this);
			else return visitor.visitChildren(this);
		}
	}

	public final CallargContext callarg() throws RecognitionException {
		CallargContext _localctx = new CallargContext(_ctx, getState());
		enterRule(_localctx, 14, RULE_callarg);
		try {
			setState(88);
			switch (_input.LA(1)) {
			case STRING:
			case NUM:
			case ID:
				_localctx = new CallargvalueContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(83);
				value();
				}
				break;
			case LPAREN:
				_localctx = new CallargevalContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(84);
				match(LPAREN);
				setState(85);
				eval();
				setState(86);
				match(RPAREN);
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class EvalContext extends ParserRuleContext {
		public BinopContext binop() {
			return getRuleContext(BinopContext.class,0);
		}
		public CallContext call() {
			return getRuleContext(CallContext.class,0);
		}
		public EvalContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_eval; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterEval(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitEval(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitEval(this);
			else return visitor.visitChildren(this);
		}
	}

	public final EvalContext eval() throws RecognitionException {
		EvalContext _localctx = new EvalContext(_ctx, getState());
		enterRule(_localctx, 16, RULE_eval);
		try {
			setState(92);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,7,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(90);
				binop();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(91);
				call();
				}
				break;
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class BinopContext extends ParserRuleContext {
		public BinopContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_binop; }
	 
		public BinopContext() { }
		public void copyFrom(BinopContext ctx) {
			super.copyFrom(ctx);
		}
	}
	public static class BinopvalueContext extends BinopContext {
		public ValueContext value() {
			return getRuleContext(ValueContext.class,0);
		}
		public BinopvalueContext(BinopContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterBinopvalue(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitBinopvalue(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitBinopvalue(this);
			else return visitor.visitChildren(this);
		}
	}
	public static class BinopbinopContext extends BinopContext {
		public ValueContext value() {
			return getRuleContext(ValueContext.class,0);
		}
		public SignContext sign() {
			return getRuleContext(SignContext.class,0);
		}
		public BinopContext binop() {
			return getRuleContext(BinopContext.class,0);
		}
		public BinopbinopContext(BinopContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterBinopbinop(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitBinopbinop(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitBinopbinop(this);
			else return visitor.visitChildren(this);
		}
	}

	public final BinopContext binop() throws RecognitionException {
		BinopContext _localctx = new BinopContext(_ctx, getState());
		enterRule(_localctx, 18, RULE_binop);
		try {
			setState(99);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,8,_ctx) ) {
			case 1:
				_localctx = new BinopvalueContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(94);
				value();
				}
				break;
			case 2:
				_localctx = new BinopbinopContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(95);
				value();
				setState(96);
				sign();
				setState(97);
				binop();
				}
				break;
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class SignContext extends ParserRuleContext {
		public TerminalNode PLUS() { return getToken(GrammarParser.PLUS, 0); }
		public TerminalNode MINUS() { return getToken(GrammarParser.MINUS, 0); }
		public TerminalNode MULT() { return getToken(GrammarParser.MULT, 0); }
		public TerminalNode DIV() { return getToken(GrammarParser.DIV, 0); }
		public TerminalNode GE() { return getToken(GrammarParser.GE, 0); }
		public TerminalNode LE() { return getToken(GrammarParser.LE, 0); }
		public TerminalNode GT() { return getToken(GrammarParser.GT, 0); }
		public TerminalNode LT() { return getToken(GrammarParser.LT, 0); }
		public TerminalNode TILDE() { return getToken(GrammarParser.TILDE, 0); }
		public SignContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_sign; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterSign(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitSign(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitSign(this);
			else return visitor.visitChildren(this);
		}
	}

	public final SignContext sign() throws RecognitionException {
		SignContext _localctx = new SignContext(_ctx, getState());
		enterRule(_localctx, 20, RULE_sign);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(101);
			_la = _input.LA(1);
			if ( !((((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << PLUS) | (1L << MINUS) | (1L << DIV) | (1L << MULT) | (1L << LE) | (1L << GE) | (1L << LT) | (1L << GT) | (1L << TILDE))) != 0)) ) {
			_errHandler.recoverInline(this);
			} else {
				consume();
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class FunctionContext extends ParserRuleContext {
		public TerminalNode LPAREN() { return getToken(GrammarParser.LPAREN, 0); }
		public TerminalNode RPAREN() { return getToken(GrammarParser.RPAREN, 0); }
		public TerminalNode ARROW() { return getToken(GrammarParser.ARROW, 0); }
		public FbodyContext fbody() {
			return getRuleContext(FbodyContext.class,0);
		}
		public List<ArgContext> arg() {
			return getRuleContexts(ArgContext.class);
		}
		public ArgContext arg(int i) {
			return getRuleContext(ArgContext.class,i);
		}
		public TerminalNode COLON() { return getToken(GrammarParser.COLON, 0); }
		public TerminalNode TYPE() { return getToken(GrammarParser.TYPE, 0); }
		public List<TerminalNode> COMMA() { return getTokens(GrammarParser.COMMA); }
		public TerminalNode COMMA(int i) {
			return getToken(GrammarParser.COMMA, i);
		}
		public FunctionContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_function; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterFunction(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitFunction(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitFunction(this);
			else return visitor.visitChildren(this);
		}
	}

	public final FunctionContext function() throws RecognitionException {
		FunctionContext _localctx = new FunctionContext(_ctx, getState());
		enterRule(_localctx, 22, RULE_function);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(103);
			match(LPAREN);
			setState(112);
			_la = _input.LA(1);
			if (_la==ID) {
				{
				setState(104);
				arg();
				setState(109);
				_errHandler.sync(this);
				_la = _input.LA(1);
				while (_la==COMMA) {
					{
					{
					setState(105);
					match(COMMA);
					setState(106);
					arg();
					}
					}
					setState(111);
					_errHandler.sync(this);
					_la = _input.LA(1);
				}
				}
			}

			setState(114);
			match(RPAREN);
			setState(117);
			_la = _input.LA(1);
			if (_la==COLON) {
				{
				setState(115);
				match(COLON);
				setState(116);
				match(TYPE);
				}
			}

			setState(119);
			match(ARROW);
			setState(120);
			fbody();
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class FbodyContext extends ParserRuleContext {
		public BlockContext block() {
			return getRuleContext(BlockContext.class,0);
		}
		public StatementContext statement() {
			return getRuleContext(StatementContext.class,0);
		}
		public FbodyContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_fbody; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterFbody(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitFbody(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitFbody(this);
			else return visitor.visitChildren(this);
		}
	}

	public final FbodyContext fbody() throws RecognitionException {
		FbodyContext _localctx = new FbodyContext(_ctx, getState());
		enterRule(_localctx, 24, RULE_fbody);
		try {
			setState(124);
			switch (_input.LA(1)) {
			case LBRACKET:
				enterOuterAlt(_localctx, 1);
				{
				setState(122);
				block();
				}
				break;
			case IF:
			case VAR:
			case RETURN:
			case ID:
				enterOuterAlt(_localctx, 2);
				{
				setState(123);
				statement();
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class ArgContext extends ParserRuleContext {
		public TerminalNode ID() { return getToken(GrammarParser.ID, 0); }
		public TerminalNode COLON() { return getToken(GrammarParser.COLON, 0); }
		public TerminalNode TYPE() { return getToken(GrammarParser.TYPE, 0); }
		public ArgContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_arg; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterArg(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitArg(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitArg(this);
			else return visitor.visitChildren(this);
		}
	}

	public final ArgContext arg() throws RecognitionException {
		ArgContext _localctx = new ArgContext(_ctx, getState());
		enterRule(_localctx, 26, RULE_arg);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(126);
			match(ID);
			setState(127);
			match(COLON);
			setState(128);
			match(TYPE);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class BlockContext extends ParserRuleContext {
		public TerminalNode LBRACKET() { return getToken(GrammarParser.LBRACKET, 0); }
		public TerminalNode RBRACKET() { return getToken(GrammarParser.RBRACKET, 0); }
		public List<StatementContext> statement() {
			return getRuleContexts(StatementContext.class);
		}
		public StatementContext statement(int i) {
			return getRuleContext(StatementContext.class,i);
		}
		public List<TerminalNode> SEMICOLON() { return getTokens(GrammarParser.SEMICOLON); }
		public TerminalNode SEMICOLON(int i) {
			return getToken(GrammarParser.SEMICOLON, i);
		}
		public BlockContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_block; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterBlock(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitBlock(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitBlock(this);
			else return visitor.visitChildren(this);
		}
	}

	public final BlockContext block() throws RecognitionException {
		BlockContext _localctx = new BlockContext(_ctx, getState());
		enterRule(_localctx, 28, RULE_block);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(130);
			match(LBRACKET);
			setState(134); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(131);
				statement();
				setState(132);
				match(SEMICOLON);
				}
				}
				setState(136); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( (((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << IF) | (1L << VAR) | (1L << RETURN) | (1L << ID))) != 0) );
			setState(138);
			match(RBRACKET);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static class ValueContext extends ParserRuleContext {
		public ValueContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_value; }
	 
		public ValueContext() { }
		public void copyFrom(ValueContext ctx) {
			super.copyFrom(ctx);
		}
	}
	public static class ValueIDContext extends ValueContext {
		public TerminalNode ID() { return getToken(GrammarParser.ID, 0); }
		public ValueIDContext(ValueContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterValueID(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitValueID(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitValueID(this);
			else return visitor.visitChildren(this);
		}
	}
	public static class ValueSTRINGContext extends ValueContext {
		public TerminalNode STRING() { return getToken(GrammarParser.STRING, 0); }
		public ValueSTRINGContext(ValueContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterValueSTRING(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitValueSTRING(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitValueSTRING(this);
			else return visitor.visitChildren(this);
		}
	}
	public static class ValueNUMContext extends ValueContext {
		public TerminalNode NUM() { return getToken(GrammarParser.NUM, 0); }
		public ValueNUMContext(ValueContext ctx) { copyFrom(ctx); }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).enterValueNUM(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof GrammarListener ) ((GrammarListener)listener).exitValueNUM(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof GrammarVisitor ) return ((GrammarVisitor<? extends T>)visitor).visitValueNUM(this);
			else return visitor.visitChildren(this);
		}
	}

	public final ValueContext value() throws RecognitionException {
		ValueContext _localctx = new ValueContext(_ctx, getState());
		enterRule(_localctx, 30, RULE_value);
		try {
			setState(143);
			switch (_input.LA(1)) {
			case ID:
				_localctx = new ValueIDContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(140);
				match(ID);
				}
				break;
			case NUM:
				_localctx = new ValueNUMContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(141);
				match(NUM);
				}
				break;
			case STRING:
				_localctx = new ValueSTRINGContext(_localctx);
				enterOuterAlt(_localctx, 3);
				{
				setState(142);
				match(STRING);
				}
				break;
			default:
				throw new NoViableAltException(this);
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static final String _serializedATN =
		"\3\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd\3\35\u0094\4\2\t\2"+
		"\4\3\t\3\4\4\t\4\4\5\t\5\4\6\t\6\4\7\t\7\4\b\t\b\4\t\t\t\4\n\t\n\4\13"+
		"\t\13\4\f\t\f\4\r\t\r\4\16\t\16\4\17\t\17\4\20\t\20\4\21\t\21\3\2\3\2"+
		"\3\2\3\3\3\3\3\3\6\3)\n\3\r\3\16\3*\3\4\3\4\3\4\3\4\5\4\61\n\4\3\5\3\5"+
		"\3\5\3\5\5\5\67\n\5\3\5\3\5\3\5\3\5\3\5\3\5\5\5?\n\5\3\5\3\5\5\5C\n\5"+
		"\3\6\3\6\3\6\3\6\3\6\3\6\3\7\3\7\3\7\3\b\3\b\3\b\7\bQ\n\b\f\b\16\bT\13"+
		"\b\3\t\3\t\3\t\3\t\3\t\5\t[\n\t\3\n\3\n\5\n_\n\n\3\13\3\13\3\13\3\13\3"+
		"\13\5\13f\n\13\3\f\3\f\3\r\3\r\3\r\3\r\7\rn\n\r\f\r\16\rq\13\r\5\rs\n"+
		"\r\3\r\3\r\3\r\5\rx\n\r\3\r\3\r\3\r\3\16\3\16\5\16\177\n\16\3\17\3\17"+
		"\3\17\3\17\3\20\3\20\3\20\3\20\6\20\u0089\n\20\r\20\16\20\u008a\3\20\3"+
		"\20\3\21\3\21\3\21\5\21\u0092\n\21\3\21\2\2\22\2\4\6\b\n\f\16\20\22\24"+
		"\26\30\32\34\36 \2\3\3\2\n\22\u0095\2\"\3\2\2\2\4(\3\2\2\2\6\60\3\2\2"+
		"\2\bB\3\2\2\2\nD\3\2\2\2\fJ\3\2\2\2\16M\3\2\2\2\20Z\3\2\2\2\22^\3\2\2"+
		"\2\24e\3\2\2\2\26g\3\2\2\2\30i\3\2\2\2\32~\3\2\2\2\34\u0080\3\2\2\2\36"+
		"\u0084\3\2\2\2 \u0091\3\2\2\2\"#\5\4\3\2#$\7\2\2\3$\3\3\2\2\2%&\5\6\4"+
		"\2&\'\7\33\2\2\')\3\2\2\2(%\3\2\2\2)*\3\2\2\2*(\3\2\2\2*+\3\2\2\2+\5\3"+
		"\2\2\2,\61\5\b\5\2-\61\5\n\6\2.\61\5\f\7\2/\61\5\16\b\2\60,\3\2\2\2\60"+
		"-\3\2\2\2\60.\3\2\2\2\60/\3\2\2\2\61\7\3\2\2\2\62\63\7\25\2\2\63\66\7"+
		"\32\2\2\64\65\7\34\2\2\65\67\7\3\2\2\66\64\3\2\2\2\66\67\3\2\2\2\678\3"+
		"\2\2\289\7\27\2\29C\5\22\n\2:;\7\25\2\2;>\7\32\2\2<=\7\34\2\2=?\7\3\2"+
		"\2><\3\2\2\2>?\3\2\2\2?@\3\2\2\2@A\7\27\2\2AC\5\30\r\2B\62\3\2\2\2B:\3"+
		"\2\2\2C\t\3\2\2\2DE\7\24\2\2EF\7\5\2\2FG\5\24\13\2GH\7\6\2\2HI\5\36\20"+
		"\2I\13\3\2\2\2JK\7\26\2\2KL\5\22\n\2L\r\3\2\2\2MN\7\32\2\2NR\7\34\2\2"+
		"OQ\5\20\t\2PO\3\2\2\2QT\3\2\2\2RP\3\2\2\2RS\3\2\2\2S\17\3\2\2\2TR\3\2"+
		"\2\2U[\5 \21\2VW\7\5\2\2WX\5\22\n\2XY\7\6\2\2Y[\3\2\2\2ZU\3\2\2\2ZV\3"+
		"\2\2\2[\21\3\2\2\2\\_\5\24\13\2]_\5\16\b\2^\\\3\2\2\2^]\3\2\2\2_\23\3"+
		"\2\2\2`f\5 \21\2ab\5 \21\2bc\5\26\f\2cd\5\24\13\2df\3\2\2\2e`\3\2\2\2"+
		"ea\3\2\2\2f\25\3\2\2\2gh\t\2\2\2h\27\3\2\2\2ir\7\5\2\2jo\5\34\17\2kl\7"+
		"\30\2\2ln\5\34\17\2mk\3\2\2\2nq\3\2\2\2om\3\2\2\2op\3\2\2\2ps\3\2\2\2"+
		"qo\3\2\2\2rj\3\2\2\2rs\3\2\2\2st\3\2\2\2tw\7\6\2\2uv\7\34\2\2vx\7\3\2"+
		"\2wu\3\2\2\2wx\3\2\2\2xy\3\2\2\2yz\7\23\2\2z{\5\32\16\2{\31\3\2\2\2|\177"+
		"\5\36\20\2}\177\5\6\4\2~|\3\2\2\2~}\3\2\2\2\177\33\3\2\2\2\u0080\u0081"+
		"\7\32\2\2\u0081\u0082\7\34\2\2\u0082\u0083\7\3\2\2\u0083\35\3\2\2\2\u0084"+
		"\u0088\7\b\2\2\u0085\u0086\5\6\4\2\u0086\u0087\7\33\2\2\u0087\u0089\3"+
		"\2\2\2\u0088\u0085\3\2\2\2\u0089\u008a\3\2\2\2\u008a\u0088\3\2\2\2\u008a"+
		"\u008b\3\2\2\2\u008b\u008c\3\2\2\2\u008c\u008d\7\t\2\2\u008d\37\3\2\2"+
		"\2\u008e\u0092\7\32\2\2\u008f\u0092\7\31\2\2\u0090\u0092\7\4\2\2\u0091"+
		"\u008e\3\2\2\2\u0091\u008f\3\2\2\2\u0091\u0090\3\2\2\2\u0092!\3\2\2\2"+
		"\21*\60\66>BRZ^eorw~\u008a\u0091";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}