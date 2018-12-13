var GrammarVisitor = require('./GrammarVisitor').GrammarVisitor;

class visitor extends GrammarVisitor {

    visitProgram (ctx) {
        var children = []
        var size = ctx.statement().length
        for (var c in ctx.statement()) {
            children.push(this.visit(c))
        }
        return {type: "program", children: children}
    }


    visitAssignstatement(ctx) {
        return this.visit(ctx.assign())
    }



    visitIfstatement(ctx) {
        return this.visit(ctx.iff())
    }



    visitReturnstatement(ctx) {
        return this.visit(ctx.returnn())
    }



    visitCallstatement(ctx) {
        return this.visit(ctx.call())
    }



    visitAssigneval(ctx) {
        return {type: "assign",
                name: ctx.ID.toString(),
                nstype:ctx.TYPE.toString(),
                value: this.visit(ctx.eval())}
    }



    visitAssignfunction(ctx) {
        return {type: "assignfunc",
            name: ctx.ID.toString(),
            nstype:ctx.TYPE.toString(),
            value: this.visit(ctx.functionn())}
    }



    visitIff(ctx) {
        return {type: "if",
                cond:this.visit(ctx.binop()),
                body:this.visit(ctx.block())}
    }



    visitReturnn(ctx) {
        return {type: "return",
                value: ctx.eval()}
    }



    visitCall(ctx) {
        var children = []
        for (var c in ctx.value()) {
            children.push(this.visit(c))
        }
        for (var c in ctx.eval()) {
            children.push(this.visit(c))
        }
        return {type: "call",
                name: ctx.ID,
                args: children}
    }



    visitEvalbinop(ctx) {
        return {t: 4}
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