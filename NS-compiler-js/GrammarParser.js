// Generated from Grammar.g4 by ANTLR 4.5.3
// jshint ignore: start
var antlr4 = require('antlr4/index');
var GrammarListener = require('./GrammarListener').GrammarListener;
var GrammarVisitor = require('./GrammarVisitor').GrammarVisitor;

var grammarFileName = "Grammar.g4";

var serializedATN = ["\u0003\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd",
    "\u0003\u001c\u0086\u0004\u0002\t\u0002\u0004\u0003\t\u0003\u0004\u0004",
    "\t\u0004\u0004\u0005\t\u0005\u0004\u0006\t\u0006\u0004\u0007\t\u0007",
    "\u0004\b\t\b\u0004\t\t\t\u0004\n\t\n\u0004\u000b\t\u000b\u0004\f\t\f",
    "\u0004\r\t\r\u0004\u000e\t\u000e\u0004\u000f\t\u000f\u0003\u0002\u0003",
    "\u0002\u0003\u0002\u0003\u0003\u0003\u0003\u0003\u0003\u0006\u0003%",
    "\n\u0003\r\u0003\u000e\u0003&\u0003\u0004\u0003\u0004\u0003\u0004\u0003",
    "\u0004\u0005\u0004-\n\u0004\u0003\u0005\u0003\u0005\u0003\u0005\u0003",
    "\u0005\u0005\u00053\n\u0005\u0003\u0005\u0003\u0005\u0003\u0005\u0003",
    "\u0005\u0003\u0005\u0003\u0005\u0005\u0005;\n\u0005\u0003\u0005\u0003",
    "\u0005\u0005\u0005?\n\u0005\u0003\u0006\u0003\u0006\u0003\u0006\u0003",
    "\u0006\u0003\u0006\u0003\u0006\u0003\u0007\u0003\u0007\u0003\u0007\u0003",
    "\b\u0003\b\u0003\b\u0003\b\u0003\b\u0003\b\u0003\b\u0007\bQ\n\b\f\b",
    "\u000e\bT\u000b\b\u0003\t\u0003\t\u0005\tX\n\t\u0003\n\u0003\n\u0003",
    "\n\u0003\n\u0003\n\u0005\n_\n\n\u0003\u000b\u0003\u000b\u0003\f\u0003",
    "\f\u0003\f\u0003\f\u0007\fg\n\f\f\f\u000e\fj\u000b\f\u0005\fl\n\f\u0003",
    "\f\u0003\f\u0003\f\u0005\fq\n\f\u0003\f\u0003\f\u0003\f\u0003\r\u0003",
    "\r\u0003\r\u0003\r\u0003\u000e\u0003\u000e\u0003\u000e\u0003\u000e\u0006",
    "\u000e~\n\u000e\r\u000e\u000e\u000e\u007f\u0003\u000e\u0003\u000e\u0003",
    "\u000f\u0003\u000f\u0003\u000f\u0002\u0002\u0010\u0002\u0004\u0006\b",
    "\n\f\u000e\u0010\u0012\u0014\u0016\u0018\u001a\u001c\u0002\u0004\u0003",
    "\u0002\n\u0011\u0004\u0002\u0004\u0004\u0018\u0019\u0086\u0002\u001e",
    "\u0003\u0002\u0002\u0002\u0004$\u0003\u0002\u0002\u0002\u0006,\u0003",
    "\u0002\u0002\u0002\b>\u0003\u0002\u0002\u0002\n@\u0003\u0002\u0002\u0002",
    "\fF\u0003\u0002\u0002\u0002\u000eI\u0003\u0002\u0002\u0002\u0010W\u0003",
    "\u0002\u0002\u0002\u0012^\u0003\u0002\u0002\u0002\u0014`\u0003\u0002",
    "\u0002\u0002\u0016b\u0003\u0002\u0002\u0002\u0018u\u0003\u0002\u0002",
    "\u0002\u001ay\u0003\u0002\u0002\u0002\u001c\u0083\u0003\u0002\u0002",
    "\u0002\u001e\u001f\u0005\u0004\u0003\u0002\u001f \u0007\u0002\u0002",
    "\u0003 \u0003\u0003\u0002\u0002\u0002!\"\u0005\u0006\u0004\u0002\"#",
    "\u0007\u001a\u0002\u0002#%\u0003\u0002\u0002\u0002$!\u0003\u0002\u0002",
    "\u0002%&\u0003\u0002\u0002\u0002&$\u0003\u0002\u0002\u0002&\'\u0003",
    "\u0002\u0002\u0002\'\u0005\u0003\u0002\u0002\u0002(-\u0005\b\u0005\u0002",
    ")-\u0005\n\u0006\u0002*-\u0005\f\u0007\u0002+-\u0005\u000e\b\u0002,",
    "(\u0003\u0002\u0002\u0002,)\u0003\u0002\u0002\u0002,*\u0003\u0002\u0002",
    "\u0002,+\u0003\u0002\u0002\u0002-\u0007\u0003\u0002\u0002\u0002./\u0007",
    "\u0014\u0002\u0002/2\u0007\u0019\u0002\u000201\u0007\u001b\u0002\u0002",
    "13\u0007\u0003\u0002\u000220\u0003\u0002\u0002\u000223\u0003\u0002\u0002",
    "\u000234\u0003\u0002\u0002\u000245\u0007\u0016\u0002\u00025?\u0005\u0010",
    "\t\u000267\u0007\u0014\u0002\u00027:\u0007\u0019\u0002\u000289\u0007",
    "\u001b\u0002\u00029;\u0007\u0003\u0002\u0002:8\u0003\u0002\u0002\u0002",
    ":;\u0003\u0002\u0002\u0002;<\u0003\u0002\u0002\u0002<=\u0007\u0016\u0002",
    "\u0002=?\u0005\u0016\f\u0002>.\u0003\u0002\u0002\u0002>6\u0003\u0002",
    "\u0002\u0002?\t\u0003\u0002\u0002\u0002@A\u0007\u0013\u0002\u0002AB",
    "\u0007\u0005\u0002\u0002BC\u0005\u0012\n\u0002CD\u0007\u0006\u0002\u0002",
    "DE\u0005\u001a\u000e\u0002E\u000b\u0003\u0002\u0002\u0002FG\u0007\u0015",
    "\u0002\u0002GH\u0005\u0010\t\u0002H\r\u0003\u0002\u0002\u0002IJ\u0007",
    "\u0019\u0002\u0002JR\u0007\u001b\u0002\u0002KQ\u0005\u001c\u000f\u0002",
    "LM\u0007\u0005\u0002\u0002MN\u0005\u0010\t\u0002NO\u0007\u0006\u0002",
    "\u0002OQ\u0003\u0002\u0002\u0002PK\u0003\u0002\u0002\u0002PL\u0003\u0002",
    "\u0002\u0002QT\u0003\u0002\u0002\u0002RP\u0003\u0002\u0002\u0002RS\u0003",
    "\u0002\u0002\u0002S\u000f\u0003\u0002\u0002\u0002TR\u0003\u0002\u0002",
    "\u0002UX\u0005\u0012\n\u0002VX\u0005\u000e\b\u0002WU\u0003\u0002\u0002",
    "\u0002WV\u0003\u0002\u0002\u0002X\u0011\u0003\u0002\u0002\u0002Y_\u0005",
    "\u001c\u000f\u0002Z[\u0005\u001c\u000f\u0002[\\\u0005\u0014\u000b\u0002",
    "\\]\u0005\u0012\n\u0002]_\u0003\u0002\u0002\u0002^Y\u0003\u0002\u0002",
    "\u0002^Z\u0003\u0002\u0002\u0002_\u0013\u0003\u0002\u0002\u0002`a\t",
    "\u0002\u0002\u0002a\u0015\u0003\u0002\u0002\u0002bk\u0007\u0005\u0002",
    "\u0002ch\u0005\u0018\r\u0002de\u0007\u0017\u0002\u0002eg\u0005\u0018",
    "\r\u0002fd\u0003\u0002\u0002\u0002gj\u0003\u0002\u0002\u0002hf\u0003",
    "\u0002\u0002\u0002hi\u0003\u0002\u0002\u0002il\u0003\u0002\u0002\u0002",
    "jh\u0003\u0002\u0002\u0002kc\u0003\u0002\u0002\u0002kl\u0003\u0002\u0002",
    "\u0002lm\u0003\u0002\u0002\u0002mp\u0007\u0006\u0002\u0002no\u0007\u001b",
    "\u0002\u0002oq\u0007\u0003\u0002\u0002pn\u0003\u0002\u0002\u0002pq\u0003",
    "\u0002\u0002\u0002qr\u0003\u0002\u0002\u0002rs\u0007\u0012\u0002\u0002",
    "st\u0005\u001a\u000e\u0002t\u0017\u0003\u0002\u0002\u0002uv\u0007\u0019",
    "\u0002\u0002vw\u0007\u001b\u0002\u0002wx\u0007\u0003\u0002\u0002x\u0019",
    "\u0003\u0002\u0002\u0002y}\u0007\b\u0002\u0002z{\u0005\u0006\u0004\u0002",
    "{|\u0007\u001a\u0002\u0002|~\u0003\u0002\u0002\u0002}z\u0003\u0002\u0002",
    "\u0002~\u007f\u0003\u0002\u0002\u0002\u007f}\u0003\u0002\u0002\u0002",
    "\u007f\u0080\u0003\u0002\u0002\u0002\u0080\u0081\u0003\u0002\u0002\u0002",
    "\u0081\u0082\u0007\t\u0002\u0002\u0082\u001b\u0003\u0002\u0002\u0002",
    "\u0083\u0084\t\u0003\u0002\u0002\u0084\u001d\u0003\u0002\u0002\u0002",
    "\u000f&,2:>PRW^hkp\u007f"].join("");


var atn = new antlr4.atn.ATNDeserializer().deserialize(serializedATN);

var decisionsToDFA = atn.decisionToState.map( function(ds, index) { return new antlr4.dfa.DFA(ds, index); });

var sharedContextCache = new antlr4.PredictionContextCache();

var literalNames = [ null, null, null, "'('", "')'", "'\"'", "'{'", "'}'", 
                     "'+'", "'-'", "'/'", "'*'", "'<='", "'>='", "'<'", 
                     "'>'", "'=>'", "'if'", "'var'", "'return'", "'='", 
                     "','", null, null, "';'", "':'" ];

var symbolicNames = [ null, "TYPE", "STRING", "LPAREN", "RPAREN", "QUOTE", 
                      "LBRACKET", "RBRACKET", "PLUS", "MINUS", "DIV", "MULT", 
                      "LE", "GE", "LT", "GT", "ARROW", "IF", "VAR", "RETURN", 
                      "EQ", "COMMA", "NUM", "ID", "SEMICOLON", "COLON", 
                      "WS" ];

var ruleNames =  [ "start", "program", "statement", "assign", "iff", "returnn", 
                   "call", "eval", "binop", "sign", "functionn", "arg", 
                   "block", "value" ];

function GrammarParser (input) {
	antlr4.Parser.call(this, input);
    this._interp = new antlr4.atn.ParserATNSimulator(this, atn, decisionsToDFA, sharedContextCache);
    this.ruleNames = ruleNames;
    this.literalNames = literalNames;
    this.symbolicNames = symbolicNames;
    return this;
}

GrammarParser.prototype = Object.create(antlr4.Parser.prototype);
GrammarParser.prototype.constructor = GrammarParser;

Object.defineProperty(GrammarParser.prototype, "atn", {
	get : function() {
		return atn;
	}
});

GrammarParser.EOF = antlr4.Token.EOF;
GrammarParser.TYPE = 1;
GrammarParser.STRING = 2;
GrammarParser.LPAREN = 3;
GrammarParser.RPAREN = 4;
GrammarParser.QUOTE = 5;
GrammarParser.LBRACKET = 6;
GrammarParser.RBRACKET = 7;
GrammarParser.PLUS = 8;
GrammarParser.MINUS = 9;
GrammarParser.DIV = 10;
GrammarParser.MULT = 11;
GrammarParser.LE = 12;
GrammarParser.GE = 13;
GrammarParser.LT = 14;
GrammarParser.GT = 15;
GrammarParser.ARROW = 16;
GrammarParser.IF = 17;
GrammarParser.VAR = 18;
GrammarParser.RETURN = 19;
GrammarParser.EQ = 20;
GrammarParser.COMMA = 21;
GrammarParser.NUM = 22;
GrammarParser.ID = 23;
GrammarParser.SEMICOLON = 24;
GrammarParser.COLON = 25;
GrammarParser.WS = 26;

GrammarParser.RULE_start = 0;
GrammarParser.RULE_program = 1;
GrammarParser.RULE_statement = 2;
GrammarParser.RULE_assign = 3;
GrammarParser.RULE_iff = 4;
GrammarParser.RULE_returnn = 5;
GrammarParser.RULE_call = 6;
GrammarParser.RULE_eval = 7;
GrammarParser.RULE_binop = 8;
GrammarParser.RULE_sign = 9;
GrammarParser.RULE_functionn = 10;
GrammarParser.RULE_arg = 11;
GrammarParser.RULE_block = 12;
GrammarParser.RULE_value = 13;

function StartContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_start;
    return this;
}

StartContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
StartContext.prototype.constructor = StartContext;

StartContext.prototype.program = function() {
    return this.getTypedRuleContext(ProgramContext,0);
};

StartContext.prototype.EOF = function() {
    return this.getToken(GrammarParser.EOF, 0);
};

StartContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterStart(this);
	}
};

StartContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitStart(this);
	}
};

StartContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitStart(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.StartContext = StartContext;

GrammarParser.prototype.start = function() {

    var localctx = new StartContext(this, this._ctx, this.state);
    this.enterRule(localctx, 0, GrammarParser.RULE_start);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 28;
        this.program();
        this.state = 29;
        this.match(GrammarParser.EOF);
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function ProgramContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_program;
    return this;
}

ProgramContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
ProgramContext.prototype.constructor = ProgramContext;

ProgramContext.prototype.statement = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(StatementContext);
    } else {
        return this.getTypedRuleContext(StatementContext,i);
    }
};

ProgramContext.prototype.SEMICOLON = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(GrammarParser.SEMICOLON);
    } else {
        return this.getToken(GrammarParser.SEMICOLON, i);
    }
};


ProgramContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterProgram(this);
	}
};

ProgramContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitProgram(this);
	}
};

ProgramContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitProgram(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.ProgramContext = ProgramContext;

GrammarParser.prototype.program = function() {

    var localctx = new ProgramContext(this, this._ctx, this.state);
    this.enterRule(localctx, 2, GrammarParser.RULE_program);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 34; 
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        do {
            this.state = 31;
            this.statement();
            this.state = 32;
            this.match(GrammarParser.SEMICOLON);
            this.state = 36; 
            this._errHandler.sync(this);
            _la = this._input.LA(1);
        } while((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << GrammarParser.IF) | (1 << GrammarParser.VAR) | (1 << GrammarParser.RETURN) | (1 << GrammarParser.ID))) !== 0));
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function StatementContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_statement;
    return this;
}

StatementContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
StatementContext.prototype.constructor = StatementContext;


 
StatementContext.prototype.copyFrom = function(ctx) {
    antlr4.ParserRuleContext.prototype.copyFrom.call(this, ctx);
};


function IfstatementContext(parser, ctx) {
	StatementContext.call(this, parser);
    StatementContext.prototype.copyFrom.call(this, ctx);
    return this;
}

IfstatementContext.prototype = Object.create(StatementContext.prototype);
IfstatementContext.prototype.constructor = IfstatementContext;

GrammarParser.IfstatementContext = IfstatementContext;

IfstatementContext.prototype.iff = function() {
    return this.getTypedRuleContext(IffContext,0);
};
IfstatementContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterIfstatement(this);
	}
};

IfstatementContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitIfstatement(this);
	}
};

IfstatementContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitIfstatement(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function AssignstatementContext(parser, ctx) {
	StatementContext.call(this, parser);
    StatementContext.prototype.copyFrom.call(this, ctx);
    return this;
}

AssignstatementContext.prototype = Object.create(StatementContext.prototype);
AssignstatementContext.prototype.constructor = AssignstatementContext;

GrammarParser.AssignstatementContext = AssignstatementContext;

AssignstatementContext.prototype.assign = function() {
    return this.getTypedRuleContext(AssignContext,0);
};
AssignstatementContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterAssignstatement(this);
	}
};

AssignstatementContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitAssignstatement(this);
	}
};

AssignstatementContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitAssignstatement(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function CallstatementContext(parser, ctx) {
	StatementContext.call(this, parser);
    StatementContext.prototype.copyFrom.call(this, ctx);
    return this;
}

CallstatementContext.prototype = Object.create(StatementContext.prototype);
CallstatementContext.prototype.constructor = CallstatementContext;

GrammarParser.CallstatementContext = CallstatementContext;

CallstatementContext.prototype.call = function() {
    return this.getTypedRuleContext(CallContext,0);
};
CallstatementContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterCallstatement(this);
	}
};

CallstatementContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitCallstatement(this);
	}
};

CallstatementContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitCallstatement(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function ReturnstatementContext(parser, ctx) {
	StatementContext.call(this, parser);
    StatementContext.prototype.copyFrom.call(this, ctx);
    return this;
}

ReturnstatementContext.prototype = Object.create(StatementContext.prototype);
ReturnstatementContext.prototype.constructor = ReturnstatementContext;

GrammarParser.ReturnstatementContext = ReturnstatementContext;

ReturnstatementContext.prototype.returnn = function() {
    return this.getTypedRuleContext(ReturnnContext,0);
};
ReturnstatementContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterReturnstatement(this);
	}
};

ReturnstatementContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitReturnstatement(this);
	}
};

ReturnstatementContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitReturnstatement(this);
    } else {
        return visitor.visitChildren(this);
    }
};



GrammarParser.StatementContext = StatementContext;

GrammarParser.prototype.statement = function() {

    var localctx = new StatementContext(this, this._ctx, this.state);
    this.enterRule(localctx, 4, GrammarParser.RULE_statement);
    try {
        this.state = 42;
        switch(this._input.LA(1)) {
        case GrammarParser.VAR:
            localctx = new AssignstatementContext(this, localctx);
            this.enterOuterAlt(localctx, 1);
            this.state = 38;
            this.assign();
            break;
        case GrammarParser.IF:
            localctx = new IfstatementContext(this, localctx);
            this.enterOuterAlt(localctx, 2);
            this.state = 39;
            this.iff();
            break;
        case GrammarParser.RETURN:
            localctx = new ReturnstatementContext(this, localctx);
            this.enterOuterAlt(localctx, 3);
            this.state = 40;
            this.returnn();
            break;
        case GrammarParser.ID:
            localctx = new CallstatementContext(this, localctx);
            this.enterOuterAlt(localctx, 4);
            this.state = 41;
            this.call();
            break;
        default:
            throw new antlr4.error.NoViableAltException(this);
        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function AssignContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_assign;
    return this;
}

AssignContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
AssignContext.prototype.constructor = AssignContext;


 
AssignContext.prototype.copyFrom = function(ctx) {
    antlr4.ParserRuleContext.prototype.copyFrom.call(this, ctx);
};


function AssignevalContext(parser, ctx) {
	AssignContext.call(this, parser);
    AssignContext.prototype.copyFrom.call(this, ctx);
    return this;
}

AssignevalContext.prototype = Object.create(AssignContext.prototype);
AssignevalContext.prototype.constructor = AssignevalContext;

GrammarParser.AssignevalContext = AssignevalContext;

AssignevalContext.prototype.VAR = function() {
    return this.getToken(GrammarParser.VAR, 0);
};

AssignevalContext.prototype.ID = function() {
    return this.getToken(GrammarParser.ID, 0);
};

AssignevalContext.prototype.EQ = function() {
    return this.getToken(GrammarParser.EQ, 0);
};

AssignevalContext.prototype.eval = function() {
    return this.getTypedRuleContext(EvalContext,0);
};

AssignevalContext.prototype.COLON = function() {
    return this.getToken(GrammarParser.COLON, 0);
};

AssignevalContext.prototype.TYPE = function() {
    return this.getToken(GrammarParser.TYPE, 0);
};
AssignevalContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterAssigneval(this);
	}
};

AssignevalContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitAssigneval(this);
	}
};

AssignevalContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitAssigneval(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function AssignfunctionContext(parser, ctx) {
	AssignContext.call(this, parser);
    AssignContext.prototype.copyFrom.call(this, ctx);
    return this;
}

AssignfunctionContext.prototype = Object.create(AssignContext.prototype);
AssignfunctionContext.prototype.constructor = AssignfunctionContext;

GrammarParser.AssignfunctionContext = AssignfunctionContext;

AssignfunctionContext.prototype.VAR = function() {
    return this.getToken(GrammarParser.VAR, 0);
};

AssignfunctionContext.prototype.ID = function() {
    return this.getToken(GrammarParser.ID, 0);
};

AssignfunctionContext.prototype.EQ = function() {
    return this.getToken(GrammarParser.EQ, 0);
};

AssignfunctionContext.prototype.functionn = function() {
    return this.getTypedRuleContext(FunctionnContext,0);
};

AssignfunctionContext.prototype.COLON = function() {
    return this.getToken(GrammarParser.COLON, 0);
};

AssignfunctionContext.prototype.TYPE = function() {
    return this.getToken(GrammarParser.TYPE, 0);
};
AssignfunctionContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterAssignfunction(this);
	}
};

AssignfunctionContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitAssignfunction(this);
	}
};

AssignfunctionContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitAssignfunction(this);
    } else {
        return visitor.visitChildren(this);
    }
};



GrammarParser.AssignContext = AssignContext;

GrammarParser.prototype.assign = function() {

    var localctx = new AssignContext(this, this._ctx, this.state);
    this.enterRule(localctx, 6, GrammarParser.RULE_assign);
    var _la = 0; // Token type
    try {
        this.state = 60;
        this._errHandler.sync(this);
        var la_ = this._interp.adaptivePredict(this._input,4,this._ctx);
        switch(la_) {
        case 1:
            localctx = new AssignevalContext(this, localctx);
            this.enterOuterAlt(localctx, 1);
            this.state = 44;
            this.match(GrammarParser.VAR);
            this.state = 45;
            this.match(GrammarParser.ID);
            this.state = 48;
            _la = this._input.LA(1);
            if(_la===GrammarParser.COLON) {
                this.state = 46;
                this.match(GrammarParser.COLON);
                this.state = 47;
                this.match(GrammarParser.TYPE);
            }

            this.state = 50;
            this.match(GrammarParser.EQ);
            this.state = 51;
            this.eval();
            break;

        case 2:
            localctx = new AssignfunctionContext(this, localctx);
            this.enterOuterAlt(localctx, 2);
            this.state = 52;
            this.match(GrammarParser.VAR);
            this.state = 53;
            this.match(GrammarParser.ID);
            this.state = 56;
            _la = this._input.LA(1);
            if(_la===GrammarParser.COLON) {
                this.state = 54;
                this.match(GrammarParser.COLON);
                this.state = 55;
                this.match(GrammarParser.TYPE);
            }

            this.state = 58;
            this.match(GrammarParser.EQ);
            this.state = 59;
            this.functionn();
            break;

        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function IffContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_iff;
    return this;
}

IffContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
IffContext.prototype.constructor = IffContext;

IffContext.prototype.IF = function() {
    return this.getToken(GrammarParser.IF, 0);
};

IffContext.prototype.LPAREN = function() {
    return this.getToken(GrammarParser.LPAREN, 0);
};

IffContext.prototype.binop = function() {
    return this.getTypedRuleContext(BinopContext,0);
};

IffContext.prototype.RPAREN = function() {
    return this.getToken(GrammarParser.RPAREN, 0);
};

IffContext.prototype.block = function() {
    return this.getTypedRuleContext(BlockContext,0);
};

IffContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterIff(this);
	}
};

IffContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitIff(this);
	}
};

IffContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitIff(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.IffContext = IffContext;

GrammarParser.prototype.iff = function() {

    var localctx = new IffContext(this, this._ctx, this.state);
    this.enterRule(localctx, 8, GrammarParser.RULE_iff);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 62;
        this.match(GrammarParser.IF);
        this.state = 63;
        this.match(GrammarParser.LPAREN);
        this.state = 64;
        this.binop();
        this.state = 65;
        this.match(GrammarParser.RPAREN);
        this.state = 66;
        this.block();
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function ReturnnContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_returnn;
    return this;
}

ReturnnContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
ReturnnContext.prototype.constructor = ReturnnContext;

ReturnnContext.prototype.RETURN = function() {
    return this.getToken(GrammarParser.RETURN, 0);
};

ReturnnContext.prototype.eval = function() {
    return this.getTypedRuleContext(EvalContext,0);
};

ReturnnContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterReturnn(this);
	}
};

ReturnnContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitReturnn(this);
	}
};

ReturnnContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitReturnn(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.ReturnnContext = ReturnnContext;

GrammarParser.prototype.returnn = function() {

    var localctx = new ReturnnContext(this, this._ctx, this.state);
    this.enterRule(localctx, 10, GrammarParser.RULE_returnn);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 68;
        this.match(GrammarParser.RETURN);
        this.state = 69;
        this.eval();
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function CallContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_call;
    return this;
}

CallContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
CallContext.prototype.constructor = CallContext;

CallContext.prototype.ID = function() {
    return this.getToken(GrammarParser.ID, 0);
};

CallContext.prototype.COLON = function() {
    return this.getToken(GrammarParser.COLON, 0);
};

CallContext.prototype.value = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(ValueContext);
    } else {
        return this.getTypedRuleContext(ValueContext,i);
    }
};

CallContext.prototype.LPAREN = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(GrammarParser.LPAREN);
    } else {
        return this.getToken(GrammarParser.LPAREN, i);
    }
};


CallContext.prototype.eval = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(EvalContext);
    } else {
        return this.getTypedRuleContext(EvalContext,i);
    }
};

CallContext.prototype.RPAREN = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(GrammarParser.RPAREN);
    } else {
        return this.getToken(GrammarParser.RPAREN, i);
    }
};


CallContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterCall(this);
	}
};

CallContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitCall(this);
	}
};

CallContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitCall(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.CallContext = CallContext;

GrammarParser.prototype.call = function() {

    var localctx = new CallContext(this, this._ctx, this.state);
    this.enterRule(localctx, 12, GrammarParser.RULE_call);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 71;
        this.match(GrammarParser.ID);
        this.state = 72;
        this.match(GrammarParser.COLON);
        this.state = 80;
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        while((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << GrammarParser.STRING) | (1 << GrammarParser.LPAREN) | (1 << GrammarParser.NUM) | (1 << GrammarParser.ID))) !== 0)) {
            this.state = 78;
            switch(this._input.LA(1)) {
            case GrammarParser.STRING:
            case GrammarParser.NUM:
            case GrammarParser.ID:
                this.state = 73;
                this.value();
                break;
            case GrammarParser.LPAREN:
                this.state = 74;
                this.match(GrammarParser.LPAREN);
                this.state = 75;
                this.eval();
                this.state = 76;
                this.match(GrammarParser.RPAREN);
                break;
            default:
                throw new antlr4.error.NoViableAltException(this);
            }
            this.state = 82;
            this._errHandler.sync(this);
            _la = this._input.LA(1);
        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function EvalContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_eval;
    return this;
}

EvalContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
EvalContext.prototype.constructor = EvalContext;


 
EvalContext.prototype.copyFrom = function(ctx) {
    antlr4.ParserRuleContext.prototype.copyFrom.call(this, ctx);
};


function EvalbinopContext(parser, ctx) {
	EvalContext.call(this, parser);
    EvalContext.prototype.copyFrom.call(this, ctx);
    return this;
}

EvalbinopContext.prototype = Object.create(EvalContext.prototype);
EvalbinopContext.prototype.constructor = EvalbinopContext;

GrammarParser.EvalbinopContext = EvalbinopContext;

EvalbinopContext.prototype.binop = function() {
    return this.getTypedRuleContext(BinopContext,0);
};
EvalbinopContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterEvalbinop(this);
	}
};

EvalbinopContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitEvalbinop(this);
	}
};

EvalbinopContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitEvalbinop(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function EvalcallContext(parser, ctx) {
	EvalContext.call(this, parser);
    EvalContext.prototype.copyFrom.call(this, ctx);
    return this;
}

EvalcallContext.prototype = Object.create(EvalContext.prototype);
EvalcallContext.prototype.constructor = EvalcallContext;

GrammarParser.EvalcallContext = EvalcallContext;

EvalcallContext.prototype.call = function() {
    return this.getTypedRuleContext(CallContext,0);
};
EvalcallContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterEvalcall(this);
	}
};

EvalcallContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitEvalcall(this);
	}
};

EvalcallContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitEvalcall(this);
    } else {
        return visitor.visitChildren(this);
    }
};



GrammarParser.EvalContext = EvalContext;

GrammarParser.prototype.eval = function() {

    var localctx = new EvalContext(this, this._ctx, this.state);
    this.enterRule(localctx, 14, GrammarParser.RULE_eval);
    try {
        this.state = 85;
        this._errHandler.sync(this);
        var la_ = this._interp.adaptivePredict(this._input,7,this._ctx);
        switch(la_) {
        case 1:
            localctx = new EvalbinopContext(this, localctx);
            this.enterOuterAlt(localctx, 1);
            this.state = 83;
            this.binop();
            break;

        case 2:
            localctx = new EvalcallContext(this, localctx);
            this.enterOuterAlt(localctx, 2);
            this.state = 84;
            this.call();
            break;

        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function BinopContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_binop;
    return this;
}

BinopContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
BinopContext.prototype.constructor = BinopContext;


 
BinopContext.prototype.copyFrom = function(ctx) {
    antlr4.ParserRuleContext.prototype.copyFrom.call(this, ctx);
};


function BinopvalueContext(parser, ctx) {
	BinopContext.call(this, parser);
    BinopContext.prototype.copyFrom.call(this, ctx);
    return this;
}

BinopvalueContext.prototype = Object.create(BinopContext.prototype);
BinopvalueContext.prototype.constructor = BinopvalueContext;

GrammarParser.BinopvalueContext = BinopvalueContext;

BinopvalueContext.prototype.value = function() {
    return this.getTypedRuleContext(ValueContext,0);
};
BinopvalueContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterBinopvalue(this);
	}
};

BinopvalueContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitBinopvalue(this);
	}
};

BinopvalueContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitBinopvalue(this);
    } else {
        return visitor.visitChildren(this);
    }
};


function BinopbinopContext(parser, ctx) {
	BinopContext.call(this, parser);
    BinopContext.prototype.copyFrom.call(this, ctx);
    return this;
}

BinopbinopContext.prototype = Object.create(BinopContext.prototype);
BinopbinopContext.prototype.constructor = BinopbinopContext;

GrammarParser.BinopbinopContext = BinopbinopContext;

BinopbinopContext.prototype.value = function() {
    return this.getTypedRuleContext(ValueContext,0);
};

BinopbinopContext.prototype.sign = function() {
    return this.getTypedRuleContext(SignContext,0);
};

BinopbinopContext.prototype.binop = function() {
    return this.getTypedRuleContext(BinopContext,0);
};
BinopbinopContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterBinopbinop(this);
	}
};

BinopbinopContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitBinopbinop(this);
	}
};

BinopbinopContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitBinopbinop(this);
    } else {
        return visitor.visitChildren(this);
    }
};



GrammarParser.BinopContext = BinopContext;

GrammarParser.prototype.binop = function() {

    var localctx = new BinopContext(this, this._ctx, this.state);
    this.enterRule(localctx, 16, GrammarParser.RULE_binop);
    try {
        this.state = 92;
        this._errHandler.sync(this);
        var la_ = this._interp.adaptivePredict(this._input,8,this._ctx);
        switch(la_) {
        case 1:
            localctx = new BinopvalueContext(this, localctx);
            this.enterOuterAlt(localctx, 1);
            this.state = 87;
            this.value();
            break;

        case 2:
            localctx = new BinopbinopContext(this, localctx);
            this.enterOuterAlt(localctx, 2);
            this.state = 88;
            this.value();
            this.state = 89;
            this.sign();
            this.state = 90;
            this.binop();
            break;

        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function SignContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_sign;
    return this;
}

SignContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
SignContext.prototype.constructor = SignContext;

SignContext.prototype.PLUS = function() {
    return this.getToken(GrammarParser.PLUS, 0);
};

SignContext.prototype.MINUS = function() {
    return this.getToken(GrammarParser.MINUS, 0);
};

SignContext.prototype.MULT = function() {
    return this.getToken(GrammarParser.MULT, 0);
};

SignContext.prototype.DIV = function() {
    return this.getToken(GrammarParser.DIV, 0);
};

SignContext.prototype.GE = function() {
    return this.getToken(GrammarParser.GE, 0);
};

SignContext.prototype.LE = function() {
    return this.getToken(GrammarParser.LE, 0);
};

SignContext.prototype.GT = function() {
    return this.getToken(GrammarParser.GT, 0);
};

SignContext.prototype.LT = function() {
    return this.getToken(GrammarParser.LT, 0);
};

SignContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterSign(this);
	}
};

SignContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitSign(this);
	}
};

SignContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitSign(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.SignContext = SignContext;

GrammarParser.prototype.sign = function() {

    var localctx = new SignContext(this, this._ctx, this.state);
    this.enterRule(localctx, 18, GrammarParser.RULE_sign);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 94;
        _la = this._input.LA(1);
        if(!((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << GrammarParser.PLUS) | (1 << GrammarParser.MINUS) | (1 << GrammarParser.DIV) | (1 << GrammarParser.MULT) | (1 << GrammarParser.LE) | (1 << GrammarParser.GE) | (1 << GrammarParser.LT) | (1 << GrammarParser.GT))) !== 0))) {
        this._errHandler.recoverInline(this);
        }
        else {
            this.consume();
        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function FunctionnContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_functionn;
    return this;
}

FunctionnContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
FunctionnContext.prototype.constructor = FunctionnContext;

FunctionnContext.prototype.LPAREN = function() {
    return this.getToken(GrammarParser.LPAREN, 0);
};

FunctionnContext.prototype.RPAREN = function() {
    return this.getToken(GrammarParser.RPAREN, 0);
};

FunctionnContext.prototype.ARROW = function() {
    return this.getToken(GrammarParser.ARROW, 0);
};

FunctionnContext.prototype.block = function() {
    return this.getTypedRuleContext(BlockContext,0);
};

FunctionnContext.prototype.arg = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(ArgContext);
    } else {
        return this.getTypedRuleContext(ArgContext,i);
    }
};

FunctionnContext.prototype.COLON = function() {
    return this.getToken(GrammarParser.COLON, 0);
};

FunctionnContext.prototype.TYPE = function() {
    return this.getToken(GrammarParser.TYPE, 0);
};

FunctionnContext.prototype.COMMA = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(GrammarParser.COMMA);
    } else {
        return this.getToken(GrammarParser.COMMA, i);
    }
};


FunctionnContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterFunctionn(this);
	}
};

FunctionnContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitFunctionn(this);
	}
};

FunctionnContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitFunctionn(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.FunctionnContext = FunctionnContext;

GrammarParser.prototype.functionn = function() {

    var localctx = new FunctionnContext(this, this._ctx, this.state);
    this.enterRule(localctx, 20, GrammarParser.RULE_functionn);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 96;
        this.match(GrammarParser.LPAREN);
        this.state = 105;
        _la = this._input.LA(1);
        if(_la===GrammarParser.ID) {
            this.state = 97;
            this.arg();
            this.state = 102;
            this._errHandler.sync(this);
            _la = this._input.LA(1);
            while(_la===GrammarParser.COMMA) {
                this.state = 98;
                this.match(GrammarParser.COMMA);
                this.state = 99;
                this.arg();
                this.state = 104;
                this._errHandler.sync(this);
                _la = this._input.LA(1);
            }
        }

        this.state = 107;
        this.match(GrammarParser.RPAREN);
        this.state = 110;
        _la = this._input.LA(1);
        if(_la===GrammarParser.COLON) {
            this.state = 108;
            this.match(GrammarParser.COLON);
            this.state = 109;
            this.match(GrammarParser.TYPE);
        }

        this.state = 112;
        this.match(GrammarParser.ARROW);
        this.state = 113;
        this.block();
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function ArgContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_arg;
    return this;
}

ArgContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
ArgContext.prototype.constructor = ArgContext;

ArgContext.prototype.ID = function() {
    return this.getToken(GrammarParser.ID, 0);
};

ArgContext.prototype.COLON = function() {
    return this.getToken(GrammarParser.COLON, 0);
};

ArgContext.prototype.TYPE = function() {
    return this.getToken(GrammarParser.TYPE, 0);
};

ArgContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterArg(this);
	}
};

ArgContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitArg(this);
	}
};

ArgContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitArg(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.ArgContext = ArgContext;

GrammarParser.prototype.arg = function() {

    var localctx = new ArgContext(this, this._ctx, this.state);
    this.enterRule(localctx, 22, GrammarParser.RULE_arg);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 115;
        this.match(GrammarParser.ID);
        this.state = 116;
        this.match(GrammarParser.COLON);
        this.state = 117;
        this.match(GrammarParser.TYPE);
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function BlockContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_block;
    return this;
}

BlockContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
BlockContext.prototype.constructor = BlockContext;

BlockContext.prototype.LBRACKET = function() {
    return this.getToken(GrammarParser.LBRACKET, 0);
};

BlockContext.prototype.RBRACKET = function() {
    return this.getToken(GrammarParser.RBRACKET, 0);
};

BlockContext.prototype.statement = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(StatementContext);
    } else {
        return this.getTypedRuleContext(StatementContext,i);
    }
};

BlockContext.prototype.SEMICOLON = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(GrammarParser.SEMICOLON);
    } else {
        return this.getToken(GrammarParser.SEMICOLON, i);
    }
};


BlockContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterBlock(this);
	}
};

BlockContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitBlock(this);
	}
};

BlockContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitBlock(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.BlockContext = BlockContext;

GrammarParser.prototype.block = function() {

    var localctx = new BlockContext(this, this._ctx, this.state);
    this.enterRule(localctx, 24, GrammarParser.RULE_block);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 119;
        this.match(GrammarParser.LBRACKET);
        this.state = 123; 
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        do {
            this.state = 120;
            this.statement();
            this.state = 121;
            this.match(GrammarParser.SEMICOLON);
            this.state = 125; 
            this._errHandler.sync(this);
            _la = this._input.LA(1);
        } while((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << GrammarParser.IF) | (1 << GrammarParser.VAR) | (1 << GrammarParser.RETURN) | (1 << GrammarParser.ID))) !== 0));
        this.state = 127;
        this.match(GrammarParser.RBRACKET);
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};

function ValueContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = GrammarParser.RULE_value;
    return this;
}

ValueContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
ValueContext.prototype.constructor = ValueContext;

ValueContext.prototype.ID = function() {
    return this.getToken(GrammarParser.ID, 0);
};

ValueContext.prototype.NUM = function() {
    return this.getToken(GrammarParser.NUM, 0);
};

ValueContext.prototype.STRING = function() {
    return this.getToken(GrammarParser.STRING, 0);
};

ValueContext.prototype.enterRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.enterValue(this);
	}
};

ValueContext.prototype.exitRule = function(listener) {
    if(listener instanceof GrammarListener ) {
        listener.exitValue(this);
	}
};

ValueContext.prototype.accept = function(visitor) {
    if ( visitor instanceof GrammarVisitor ) {
        return visitor.visitValue(this);
    } else {
        return visitor.visitChildren(this);
    }
};




GrammarParser.ValueContext = ValueContext;

GrammarParser.prototype.value = function() {

    var localctx = new ValueContext(this, this._ctx, this.state);
    this.enterRule(localctx, 26, GrammarParser.RULE_value);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 129;
        _la = this._input.LA(1);
        if(!((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << GrammarParser.STRING) | (1 << GrammarParser.NUM) | (1 << GrammarParser.ID))) !== 0))) {
        this._errHandler.recoverInline(this);
        }
        else {
            this.consume();
        }
    } catch (re) {
    	if(re instanceof antlr4.error.RecognitionException) {
	        localctx.exception = re;
	        this._errHandler.reportError(this, re);
	        this._errHandler.recover(this, re);
	    } else {
	    	throw re;
	    }
    } finally {
        this.exitRule();
    }
    return localctx;
};


exports.GrammarParser = GrammarParser;
