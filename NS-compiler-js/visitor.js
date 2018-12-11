var GrammarVisitor = require('GrammarVisitor').GrammarVisitor;

function GrammarVisitor() {
    antlr4.tree.ParseTreeVisitor.call(this);
    return this;
}

GrammarVisitor.prototype = Object.create(antlr4.tree.ParseTreeVisitor.prototype);
GrammarVisitor.prototype.constructor = GrammarVisitor;

GrammarVisitor.prototype.visitProgram = function(ctx) {
    console.log(ctx.toString())
    console.log(ctx.statement())
};