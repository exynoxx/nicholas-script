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
	 * Enter a parse tree produced by the {@code assignstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void enterAssignstatement(GrammarParser.AssignstatementContext ctx);
	/**
	 * Exit a parse tree produced by the {@code assignstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void exitAssignstatement(GrammarParser.AssignstatementContext ctx);
	/**
	 * Enter a parse tree produced by the {@code ififstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void enterIfifstatement(GrammarParser.IfifstatementContext ctx);
	/**
	 * Exit a parse tree produced by the {@code ififstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void exitIfifstatement(GrammarParser.IfifstatementContext ctx);
	/**
	 * Enter a parse tree produced by the {@code returnstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void enterReturnstatement(GrammarParser.ReturnstatementContext ctx);
	/**
	 * Exit a parse tree produced by the {@code returnstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 */
	void exitReturnstatement(GrammarParser.ReturnstatementContext ctx);
	/**
	 * Enter a parse tree produced by the {@code assignbinop}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void enterAssignbinop(GrammarParser.AssignbinopContext ctx);
	/**
	 * Exit a parse tree produced by the {@code assignbinop}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 */
	void exitAssignbinop(GrammarParser.AssignbinopContext ctx);
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
	 * Enter a parse tree produced by {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void enterBinop(GrammarParser.BinopContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 */
	void exitBinop(GrammarParser.BinopContext ctx);
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
	 * Enter a parse tree produced by {@link GrammarParser#ifstatement}.
	 * @param ctx the parse tree
	 */
	void enterIfstatement(GrammarParser.IfstatementContext ctx);
	/**
	 * Exit a parse tree produced by {@link GrammarParser#ifstatement}.
	 * @param ctx the parse tree
	 */
	void exitIfstatement(GrammarParser.IfstatementContext ctx);
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
}