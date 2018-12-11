// Generated from Grammar.g4 by ANTLR 4.5.3
// jshint ignore: start
var antlr4 = require('antlr4/index');

// This class defines a complete generic visitor for a parse tree produced by GrammarParser.

function GrammarVisitor() {
	antlr4.tree.ParseTreeVisitor.call(this);
	return this;
}

GrammarVisitor.prototype = Object.create(antlr4.tree.ParseTreeVisitor.prototype);
GrammarVisitor.prototype.constructor = GrammarVisitor;

// Visit a parse tree produced by GrammarParser#start.
GrammarVisitor.prototype.visitStart = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#program.
GrammarVisitor.prototype.visitProgram = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#assignstatement.
GrammarVisitor.prototype.visitAssignstatement = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#ifstatement.
GrammarVisitor.prototype.visitIfstatement = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#returnstatement.
GrammarVisitor.prototype.visitReturnstatement = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#callstatement.
GrammarVisitor.prototype.visitCallstatement = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#assigneval.
GrammarVisitor.prototype.visitAssigneval = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#assignfunction.
GrammarVisitor.prototype.visitAssignfunction = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#iff.
GrammarVisitor.prototype.visitIff = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#returnn.
GrammarVisitor.prototype.visitReturnn = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#call.
GrammarVisitor.prototype.visitCall = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#evalbinop.
GrammarVisitor.prototype.visitEvalbinop = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#evalcall.
GrammarVisitor.prototype.visitEvalcall = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#binopvalue.
GrammarVisitor.prototype.visitBinopvalue = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#binopbinop.
GrammarVisitor.prototype.visitBinopbinop = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#sign.
GrammarVisitor.prototype.visitSign = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#functionn.
GrammarVisitor.prototype.visitFunctionn = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#arg.
GrammarVisitor.prototype.visitArg = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#block.
GrammarVisitor.prototype.visitBlock = function(ctx) {
};


// Visit a parse tree produced by GrammarParser#value.
GrammarVisitor.prototype.visitValue = function(ctx) {
};



exports.GrammarVisitor = GrammarVisitor;