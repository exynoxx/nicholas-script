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
		PLUS=8, MINUS=9, DIV=10, MULT=11, LE=12, GE=13, LT=14, GT=15, ARROW=16, 
		IF=17, VAR=18, RETURN=19, EQ=20, COMMA=21, NUM=22, ID=23, SEMICOLON=24, 
		COLON=25, WS=26;
	public static final int
		RULE_start = 0, RULE_program = 1, RULE_statement = 2, RULE_assign = 3, 
		RULE_iff = 4, RULE_returnn = 5, RULE_call = 6, RULE_callarg = 7, RULE_eval = 8, 
		RULE_binop = 9, RULE_sign = 10, RULE_function = 11, RULE_arg = 12, RULE_block = 13, 
		RULE_value = 14;
	public static final String[] ruleNames = {
		"start", "program", "statement", "assign", "iff", "returnn", "call", "callarg", 
		"eval", "binop", "sign", "function", "arg", "block", "value"
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
			setState(30);
			program();
			setState(31);
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
			setState(36); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(33);
				statement();
				setState(34);
				match(SEMICOLON);
				}
				}
				setState(38); 
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
			setState(44);
			switch (_input.LA(1)) {
			case VAR:
				enterOuterAlt(_localctx, 1);
				{
				setState(40);
				assign();
				}
				break;
			case IF:
				enterOuterAlt(_localctx, 2);
				{
				setState(41);
				iff();
				}
				break;
			case RETURN:
				enterOuterAlt(_localctx, 3);
				{
				setState(42);
				returnn();
				}
				break;
			case ID:
				enterOuterAlt(_localctx, 4);
				{
				setState(43);
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
			setState(62);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,4,_ctx) ) {
			case 1:
				_localctx = new AssignevalContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(46);
				match(VAR);
				setState(47);
				match(ID);
				setState(50);
				_la = _input.LA(1);
				if (_la==COLON) {
					{
					setState(48);
					match(COLON);
					setState(49);
					match(TYPE);
					}
				}

				setState(52);
				match(EQ);
				setState(53);
				eval();
				}
				break;
			case 2:
				_localctx = new AssignfunctionContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(54);
				match(VAR);
				setState(55);
				match(ID);
				setState(58);
				_la = _input.LA(1);
				if (_la==COLON) {
					{
					setState(56);
					match(COLON);
					setState(57);
					match(TYPE);
					}
				}

				setState(60);
				match(EQ);
				setState(61);
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
			setState(64);
			match(IF);
			setState(65);
			match(LPAREN);
			setState(66);
			binop();
			setState(67);
			match(RPAREN);
			setState(68);
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
			setState(70);
			match(RETURN);
			setState(71);
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
			setState(73);
			match(ID);
			setState(74);
			match(COLON);
			setState(78);
			_errHandler.sync(this);
			_la = _input.LA(1);
			while ((((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << STRING) | (1L << LPAREN) | (1L << NUM) | (1L << ID))) != 0)) {
				{
				{
				setState(75);
				callarg();
				}
				}
				setState(80);
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
			setState(86);
			switch (_input.LA(1)) {
			case STRING:
			case NUM:
			case ID:
				_localctx = new CallargvalueContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(81);
				value();
				}
				break;
			case LPAREN:
				_localctx = new CallargevalContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(82);
				match(LPAREN);
				setState(83);
				eval();
				setState(84);
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
			setState(90);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,7,_ctx) ) {
			case 1:
				enterOuterAlt(_localctx, 1);
				{
				setState(88);
				binop();
				}
				break;
			case 2:
				enterOuterAlt(_localctx, 2);
				{
				setState(89);
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
			setState(97);
			_errHandler.sync(this);
			switch ( getInterpreter().adaptivePredict(_input,8,_ctx) ) {
			case 1:
				_localctx = new BinopvalueContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(92);
				value();
				}
				break;
			case 2:
				_localctx = new BinopbinopContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(93);
				value();
				setState(94);
				sign();
				setState(95);
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
			setState(99);
			_la = _input.LA(1);
			if ( !((((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << PLUS) | (1L << MINUS) | (1L << DIV) | (1L << MULT) | (1L << LE) | (1L << GE) | (1L << LT) | (1L << GT))) != 0)) ) {
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
		public BlockContext block() {
			return getRuleContext(BlockContext.class,0);
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
			setState(101);
			match(LPAREN);
			setState(110);
			_la = _input.LA(1);
			if (_la==ID) {
				{
				setState(102);
				arg();
				setState(107);
				_errHandler.sync(this);
				_la = _input.LA(1);
				while (_la==COMMA) {
					{
					{
					setState(103);
					match(COMMA);
					setState(104);
					arg();
					}
					}
					setState(109);
					_errHandler.sync(this);
					_la = _input.LA(1);
				}
				}
			}

			setState(112);
			match(RPAREN);
			setState(115);
			_la = _input.LA(1);
			if (_la==COLON) {
				{
				setState(113);
				match(COLON);
				setState(114);
				match(TYPE);
				}
			}

			setState(117);
			match(ARROW);
			setState(118);
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
		enterRule(_localctx, 24, RULE_arg);
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(120);
			match(ID);
			setState(121);
			match(COLON);
			setState(122);
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
		enterRule(_localctx, 26, RULE_block);
		int _la;
		try {
			enterOuterAlt(_localctx, 1);
			{
			setState(124);
			match(LBRACKET);
			setState(128); 
			_errHandler.sync(this);
			_la = _input.LA(1);
			do {
				{
				{
				setState(125);
				statement();
				setState(126);
				match(SEMICOLON);
				}
				}
				setState(130); 
				_errHandler.sync(this);
				_la = _input.LA(1);
			} while ( (((_la) & ~0x3f) == 0 && ((1L << _la) & ((1L << IF) | (1L << VAR) | (1L << RETURN) | (1L << ID))) != 0) );
			setState(132);
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
		enterRule(_localctx, 28, RULE_value);
		try {
			setState(137);
			switch (_input.LA(1)) {
			case ID:
				_localctx = new ValueIDContext(_localctx);
				enterOuterAlt(_localctx, 1);
				{
				setState(134);
				match(ID);
				}
				break;
			case NUM:
				_localctx = new ValueNUMContext(_localctx);
				enterOuterAlt(_localctx, 2);
				{
				setState(135);
				match(NUM);
				}
				break;
			case STRING:
				_localctx = new ValueSTRINGContext(_localctx);
				enterOuterAlt(_localctx, 3);
				{
				setState(136);
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
		"\3\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd\3\34\u008e\4\2\t\2"+
		"\4\3\t\3\4\4\t\4\4\5\t\5\4\6\t\6\4\7\t\7\4\b\t\b\4\t\t\t\4\n\t\n\4\13"+
		"\t\13\4\f\t\f\4\r\t\r\4\16\t\16\4\17\t\17\4\20\t\20\3\2\3\2\3\2\3\3\3"+
		"\3\3\3\6\3\'\n\3\r\3\16\3(\3\4\3\4\3\4\3\4\5\4/\n\4\3\5\3\5\3\5\3\5\5"+
		"\5\65\n\5\3\5\3\5\3\5\3\5\3\5\3\5\5\5=\n\5\3\5\3\5\5\5A\n\5\3\6\3\6\3"+
		"\6\3\6\3\6\3\6\3\7\3\7\3\7\3\b\3\b\3\b\7\bO\n\b\f\b\16\bR\13\b\3\t\3\t"+
		"\3\t\3\t\3\t\5\tY\n\t\3\n\3\n\5\n]\n\n\3\13\3\13\3\13\3\13\3\13\5\13d"+
		"\n\13\3\f\3\f\3\r\3\r\3\r\3\r\7\rl\n\r\f\r\16\ro\13\r\5\rq\n\r\3\r\3\r"+
		"\3\r\5\rv\n\r\3\r\3\r\3\r\3\16\3\16\3\16\3\16\3\17\3\17\3\17\3\17\6\17"+
		"\u0083\n\17\r\17\16\17\u0084\3\17\3\17\3\20\3\20\3\20\5\20\u008c\n\20"+
		"\3\20\2\2\21\2\4\6\b\n\f\16\20\22\24\26\30\32\34\36\2\3\3\2\n\21\u008f"+
		"\2 \3\2\2\2\4&\3\2\2\2\6.\3\2\2\2\b@\3\2\2\2\nB\3\2\2\2\fH\3\2\2\2\16"+
		"K\3\2\2\2\20X\3\2\2\2\22\\\3\2\2\2\24c\3\2\2\2\26e\3\2\2\2\30g\3\2\2\2"+
		"\32z\3\2\2\2\34~\3\2\2\2\36\u008b\3\2\2\2 !\5\4\3\2!\"\7\2\2\3\"\3\3\2"+
		"\2\2#$\5\6\4\2$%\7\32\2\2%\'\3\2\2\2&#\3\2\2\2\'(\3\2\2\2(&\3\2\2\2()"+
		"\3\2\2\2)\5\3\2\2\2*/\5\b\5\2+/\5\n\6\2,/\5\f\7\2-/\5\16\b\2.*\3\2\2\2"+
		".+\3\2\2\2.,\3\2\2\2.-\3\2\2\2/\7\3\2\2\2\60\61\7\24\2\2\61\64\7\31\2"+
		"\2\62\63\7\33\2\2\63\65\7\3\2\2\64\62\3\2\2\2\64\65\3\2\2\2\65\66\3\2"+
		"\2\2\66\67\7\26\2\2\67A\5\22\n\289\7\24\2\29<\7\31\2\2:;\7\33\2\2;=\7"+
		"\3\2\2<:\3\2\2\2<=\3\2\2\2=>\3\2\2\2>?\7\26\2\2?A\5\30\r\2@\60\3\2\2\2"+
		"@8\3\2\2\2A\t\3\2\2\2BC\7\23\2\2CD\7\5\2\2DE\5\24\13\2EF\7\6\2\2FG\5\34"+
		"\17\2G\13\3\2\2\2HI\7\25\2\2IJ\5\22\n\2J\r\3\2\2\2KL\7\31\2\2LP\7\33\2"+
		"\2MO\5\20\t\2NM\3\2\2\2OR\3\2\2\2PN\3\2\2\2PQ\3\2\2\2Q\17\3\2\2\2RP\3"+
		"\2\2\2SY\5\36\20\2TU\7\5\2\2UV\5\22\n\2VW\7\6\2\2WY\3\2\2\2XS\3\2\2\2"+
		"XT\3\2\2\2Y\21\3\2\2\2Z]\5\24\13\2[]\5\16\b\2\\Z\3\2\2\2\\[\3\2\2\2]\23"+
		"\3\2\2\2^d\5\36\20\2_`\5\36\20\2`a\5\26\f\2ab\5\24\13\2bd\3\2\2\2c^\3"+
		"\2\2\2c_\3\2\2\2d\25\3\2\2\2ef\t\2\2\2f\27\3\2\2\2gp\7\5\2\2hm\5\32\16"+
		"\2ij\7\27\2\2jl\5\32\16\2ki\3\2\2\2lo\3\2\2\2mk\3\2\2\2mn\3\2\2\2nq\3"+
		"\2\2\2om\3\2\2\2ph\3\2\2\2pq\3\2\2\2qr\3\2\2\2ru\7\6\2\2st\7\33\2\2tv"+
		"\7\3\2\2us\3\2\2\2uv\3\2\2\2vw\3\2\2\2wx\7\22\2\2xy\5\34\17\2y\31\3\2"+
		"\2\2z{\7\31\2\2{|\7\33\2\2|}\7\3\2\2}\33\3\2\2\2~\u0082\7\b\2\2\177\u0080"+
		"\5\6\4\2\u0080\u0081\7\32\2\2\u0081\u0083\3\2\2\2\u0082\177\3\2\2\2\u0083"+
		"\u0084\3\2\2\2\u0084\u0082\3\2\2\2\u0084\u0085\3\2\2\2\u0085\u0086\3\2"+
		"\2\2\u0086\u0087\7\t\2\2\u0087\35\3\2\2\2\u0088\u008c\7\31\2\2\u0089\u008c"+
		"\7\30\2\2\u008a\u008c\7\4\2\2\u008b\u0088\3\2\2\2\u008b\u0089\3\2\2\2"+
		"\u008b\u008a\3\2\2\2\u008c\37\3\2\2\2\20(.\64<@PX\\cmpu\u0084\u008b";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}