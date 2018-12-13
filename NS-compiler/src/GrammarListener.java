// Generated from Grammar.g4 by ANTLR 4.5.3
import org.antlr.v4.runtime.tree.ParseTreeListener;

/**
 * This interface defines a complete listener for a parse tree produced by
 * {@link GrammarParser}.
 */
public interface GrammarListener extends ParseTreeListener {
	/**
	 * Enter a parse tree produced by {@link GrammarParser#start}.
	 * @param ctx the parse tree
	 */
	void enterStart(GrammarParser.StartContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#start}.
	 * @param ctx the parse tree
	 */
	void exitStart(GrammarParser.StartContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#program}.
	 * @param ctx the parse tree
	 */
	void enterProgram(GrammarParser.ProgramContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#program}.
	 * @param ctx the parse tree
	 */
	void exitProgram(GrammarParser.ProgramContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void enterStatement(GrammarParser.StatementContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void exitStatement(GrammarParser.StatementContext ctx);
	/**
	 * Enter a parse tree produced by the {@code assigneval}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void enterAssigneval(GrammarParser.AssignevalContext ctx);
	/**
	 * Exit a parse tree produced by the {@code assigneval}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void exitAssigneval(GrammarParser.AssignevalContext ctx);
	/**
	 * Enter a parse tree produced by the {@code assignfunction}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void enterAssignfunction(GrammarParser.AssignfunctionContext ctx);
	/**
	 * Exit a parse tree produced by the {@code assignfunction}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void exitAssignfunction(GrammarParser.AssignfunctionContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#iff}.
	 * @param ctx the parse tree
	 */
	void enterIff(GrammarParser.IffContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#iff}.
	 * @param ctx the parse tree
	 */
	void exitIff(GrammarParser.IffContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#returnn}.
	 * @param ctx the parse tree
	 */
	void enterReturnn(GrammarParser.ReturnnContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#returnn}.
	 * @param ctx the parse tree
	 */
	void exitReturnn(GrammarParser.ReturnnContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#call}.
	 * @param ctx the parse tree
	 */
	void enterCall(GrammarParser.CallContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#call}.
	 * @param ctx the parse tree
	 */
	void exitCall(GrammarParser.CallContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#eval}.
	 * @param ctx the parse tree
	 */
	void enterEval(GrammarParser.EvalContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#eval}.
	 * @param ctx the parse tree
	 */
	void exitEval(GrammarParser.EvalContext ctx);
	/**
	 * Enter a parse tree produced by the {@code binopvalue}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void enterBinopvalue(GrammarParser.BinopvalueContext ctx);
	/**
	 * Exit a parse tree produced by the {@code binopvalue}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void exitBinopvalue(GrammarParser.BinopvalueContext ctx);
	/**
	 * Enter a parse tree produced by the {@code binopbinop}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void enterBinopbinop(GrammarParser.BinopbinopContext ctx);
	/**
	 * Exit a parse tree produced by the {@code binopbinop}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void exitBinopbinop(GrammarParser.BinopbinopContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#sign}.
	 * @param ctx the parse tree
	 */
	void enterSign(GrammarParser.SignContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#sign}.
	 * @param ctx the parse tree
	 */
	void exitSign(GrammarParser.SignContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#function}.
	 * @param ctx the parse tree
	 */
	void enterFunction(GrammarParser.FunctionContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#function}.
	 * @param ctx the parse tree
	 */
	void exitFunction(GrammarParser.FunctionContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#arg}.
	 * @param ctx the parse tree
	 */
	void enterArg(GrammarParser.ArgContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#arg}.
	 * @param ctx the parse tree
	 */
	void exitArg(GrammarParser.ArgContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#block}.
	 * @param ctx the parse tree
	 */
	void enterBlock(GrammarParser.BlockContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#block}.
	 * @param ctx the parse tree
	 */
	void exitBlock(GrammarParser.BlockContext ctx);
	/**
	 * Enter a parse tree produced by {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 */
	void enterValue(GrammarParser.ValueContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 */
	void exitValue(GrammarParser.ValueContext ctx);
}