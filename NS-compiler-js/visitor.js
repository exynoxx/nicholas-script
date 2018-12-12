var GrammarVisitor = require('./GrammarVisitor').GrammarVisitor;

class visitor extends GrammarVisitor {

    visitProgram (ctx) {
        return {txt: ctx.getText()}
    }

    
    visitAssignstatement(ctx) {
    }



    visitIfstatement(ctx) {
    }



    visitReturnstatement(ctx) {
    }



    visitCallstatement(ctx) {
    }



    visitAssigneval(ctx) {
    }



    visitAssignfunction(ctx) {
    }



    visitIff(ctx) {
    }



    visitReturnn(ctx) {
    }



    visitCall(ctx) {
    }



    visitEvalbinop(ctx) {
    }



    visitEvalcall(ctx) {
    }



    visitBinopvalue(ctx) {
    }



    visitBinopbinop(ctx) {
    }



    visitSign(ctx) {
    }



    visitFunctionn(ctx) {
    }



    visitArg(ctx) {
    }



    visitBlock(ctx) {
    }



    visitValue(ctx) {
    }


}

module.exports = visitor;