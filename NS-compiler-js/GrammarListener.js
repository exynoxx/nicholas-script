// Generated from Grammar.g4 by ANTLR 4.5.3
// jshint ignore: start
var antlr4 = require('antlr4/index');

// This class defines a complete listener for a parse tree produced by GrammarParser.
function GrammarListener() {
	antlr4.tree.ParseTreeListener.call(this);
	return this;
}

GrammarListener.prototype = Object.create(antlr4.tree.ParseTreeListener.prototype);
GrammarListener.prototype.constructor = GrammarListener;

// Enter a parse tree produced by GrammarParser#start.
GrammarListener.prototype.enterStart = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#start.
GrammarListener.prototype.exitStart = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#program.
GrammarListener.prototype.enterProgram = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#program.
GrammarListener.prototype.exitProgram = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#assignstatement.
GrammarListener.prototype.enterAssignstatement = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#assignstatement.
GrammarListener.prototype.exitAssignstatement = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#ifstatement.
GrammarListener.prototype.enterIfstatement = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#ifstatement.
GrammarListener.prototype.exitIfstatement = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#returnstatement.
GrammarListener.prototype.enterReturnstatement = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#returnstatement.
GrammarListener.prototype.exitReturnstatement = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#callstatement.
GrammarListener.prototype.enterCallstatement = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#callstatement.
GrammarListener.prototype.exitCallstatement = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#assigneval.
GrammarListener.prototype.enterAssigneval = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#assigneval.
GrammarListener.prototype.exitAssigneval = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#assignfunction.
GrammarListener.prototype.enterAssignfunction = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#assignfunction.
GrammarListener.prototype.exitAssignfunction = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#iff.
GrammarListener.prototype.enterIff = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#iff.
GrammarListener.prototype.exitIff = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#returnn.
GrammarListener.prototype.enterReturnn = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#returnn.
GrammarListener.prototype.exitReturnn = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#call.
GrammarListener.prototype.enterCall = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#call.
GrammarListener.prototype.exitCall = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#evalbinop.
GrammarListener.prototype.enterEvalbinop = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#evalbinop.
GrammarListener.prototype.exitEvalbinop = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#evalcall.
GrammarListener.prototype.enterEvalcall = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#evalcall.
GrammarListener.prototype.exitEvalcall = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#binopvalue.
GrammarListener.prototype.enterBinopvalue = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#binopvalue.
GrammarListener.prototype.exitBinopvalue = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#binopbinop.
GrammarListener.prototype.enterBinopbinop = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#binopbinop.
GrammarListener.prototype.exitBinopbinop = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#sign.
GrammarListener.prototype.enterSign = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#sign.
GrammarListener.prototype.exitSign = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#functionn.
GrammarListener.prototype.enterFunctionn = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#functionn.
GrammarListener.prototype.exitFunctionn = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#arg.
GrammarListener.prototype.enterArg = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#arg.
GrammarListener.prototype.exitArg = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#block.
GrammarListener.prototype.enterBlock = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#block.
GrammarListener.prototype.exitBlock = function(ctx) {
};


// Enter a parse tree produced by GrammarParser#value.
GrammarListener.prototype.enterValue = function(ctx) {
};

// Exit a parse tree produced by GrammarParser#value.
GrammarListener.prototype.exitValue = function(ctx) {
};



exports.GrammarListener = GrammarListener;