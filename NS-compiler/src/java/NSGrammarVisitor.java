package java;

import java.util.ArrayList;
import java.util.List;

public class NSGrammarVisitor extends GrammarBaseVisitor<Node> {
    @Override
    public Node visitStart(GrammarParser.StartContext ctx) {
        return this.visit(ctx.program());
    }

    public Node visitProgram(GrammarParser.ProgramContext ctx) {
        Node n = new Node(Type.PROGRAM);

        ArrayList<Node> children = new ArrayList<>();

        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            children.add(this.visit(c));
        }
        n.children = children;
        return n;
    }

    //statement ##############################

    //assign ##########################
    @Override
    public Node visitAssigneval(GrammarParser.AssignevalContext ctx) {
        Node n = new Node(Type.ASSIGN);
        n.body = this.visit(ctx.eval());
        n.ID = ctx.ID().toString();
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : "";
        return n;
    }
    @Override
    public Node visitAssignfunction(GrammarParser.AssignfunctionContext ctx) {
        Node n = new Node(Type.ASSIGN);
        n.body = this.visit(ctx.function());
        n.ID = ctx.ID().toString();
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : "";

        return n;
    }

    @Override
    public Node visitAssigninc(GrammarParser.AssignincContext ctx) {
        Node n = new Node(Type.INCOP);
        n.ID = ctx.ID().toString();
        n.body = this.visit(ctx.binop());
        n.sign = ctx.sign().getText();
        return n;
    }

    //iff ##########################################
    @Override
    public Node visitIff(GrammarParser.IffContext ctx) {
        Node n = new Node(Type.IF);
        n.cond = this.visit(ctx.binop());
        n.body = this.visit(ctx.block(0));
        if (ctx.block().size() > 1) {
            n.elsebody = this.visit(ctx.block(1));
        }
        return n;
    }
    //whilee ########################################


    @Override
    public Node visitWhilee(GrammarParser.WhileeContext ctx) {
        Node n = new Node(Type.WHILE);
        n.body = this.visit(ctx.block());
        if (ctx.binop() != null) {
            n.cond = this.visit(ctx.binop());
        }
        return n;
    }

    //return ####################################3
    @Override
    public Node visitReturnn(GrammarParser.ReturnnContext ctx) {
        Node n = new Node(Type.RETURN);
        n.body = this.visit(ctx.eval());
        return n;
    }


    //call ##############################################
    @Override
    public Node visitCall(GrammarParser.CallContext ctx) {
        Node n = new Node(Type.CALL);

        ArrayList<Node> args = new ArrayList<>();
        List<GrammarParser.CallargContext> l = ctx.callarg();
        for (int i = l.size()-1;i >= 0; i--) {
            GrammarParser.CallargContext arg = l.get(i);
            args.add(this.visit(arg));
        }
        n.args = args;
        n.ID = ctx.ID().toString();
        return n;
    }

    //callarg #############################
    @Override
    public Node visitCallargvalue(GrammarParser.CallargvalueContext ctx) {
        return this.visit(ctx.value());
    }

    @Override
    public Node visitCallargeval(GrammarParser.CallargevalContext ctx) {
        return this.visit(ctx.eval());
    }

    @Override
    public Node visitCallargcall(GrammarParser.CallargcallContext ctx) {
        return this.visit(ctx.call());
    }
//eval #############################################

    //binop #########################################
    @Override
    public Node visitBinopvalue(GrammarParser.BinopvalueContext ctx) {
        Node n = new Node(Type.BINOP);
        n.value = this.visit(ctx.value());
        return n;
    }
    @Override
    public Node visitBinopbinop(GrammarParser.BinopbinopContext ctx) {
        Node n = new Node(Type.BINOP);
        n.body = this.visit(ctx.binop());
        n.sign = ctx.sign().getText();
        n.value = this.visit(ctx.value());
        return n;
    }

    //function ####################################################
    @Override
    public Node visitFunction(GrammarParser.FunctionContext ctx) {
        Node n = new Node(Type.FUNCTION);

        ArrayList<Node> args = new ArrayList<>();
        List<GrammarParser.ArgContext> l = ctx.arg();
        for (GrammarParser.ArgContext arg : l) {
            args.add(this.visit(arg));
        }
        n.body = this.visit(ctx.fbody());
        n.args = args;
        n.nstype = (ctx.TYPE() != null) ? ctx.TYPE().toString() : "";
        return n;
    }

    @Override
    public Node visitArg(GrammarParser.ArgContext ctx) {
        Node n = new Node(Type.ARG);
        n.ID = ctx.ID().toString();
        n.nstype = ctx.TYPE().toString();
        return n;
    }

    //block ######################################################
    @Override
    public Node visitBlock(GrammarParser.BlockContext ctx) {
        Node n = new Node(Type.BLOCK);

        ArrayList<Node> children = new ArrayList<>();
        List<GrammarParser.StatementContext> l = ctx.statement();
        for (GrammarParser.StatementContext c : l) {
            children.add(this.visit(c));
        }
        n.children = children;
        return n;
    }


    @Override
    public Node visitValueID(GrammarParser.ValueIDContext ctx) {
        Node n = new Node(Type.VALUEVARIABLE);
        n.text = ctx.getText();
        return n;
    }
    @Override
    public Node visitValueNUM(GrammarParser.ValueNUMContext ctx) {
        Node n = new Node(Type.VALUE);
        n.text = ctx.getText();
        return n;
    }
    @Override
    public Node visitValueSTRING(GrammarParser.ValueSTRINGContext ctx) {
        Node n = new Node(Type.VALUESTRING);
        n.text = ctx.getText();
        return n;
    }
}
