// Generated from Grammar.g4 by ANTLR 4.5.3
import org.antlr.v4.runtime.tree.ParseTreeVisitor;

/**
 * This interface defines a complete generic visitor for a parse tree produced
 * by {@link GrammarParser}.
 *
 * @param <T> The return type of the visit operation. Use {@link Void} for
 * operations with no return type.
 */
public interface GrammarVisitor<T> extends ParseTreeVisitor<T> {
	/**
	 * Visit a parse tree produced by {@link GrammarParser#start}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitStart(GrammarParser.StartContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#program}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitProgram(GrammarParser.ProgramContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitStatement(GrammarParser.StatementContext ctx);
	/**
	 * Visit a parse tree produced by the {@code assigneval}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitAssigneval(GrammarParser.AssignevalContext ctx);
	/**
	 * Visit a parse tree produced by the {@code assignfunction}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitAssignfunction(GrammarParser.AssignfunctionContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#iff}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitIff(GrammarParser.IffContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#returnn}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitReturnn(GrammarParser.ReturnnContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#call}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitCall(GrammarParser.CallContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#eval}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitEval(GrammarParser.EvalContext ctx);
	/**
	 * Visit a parse tree produced by the {@code binopvalue}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitBinopvalue(GrammarParser.BinopvalueContext ctx);
	/**
	 * Visit a parse tree produced by the {@code binopbinop}
	 * labeled alternative in {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitBinopbinop(GrammarParser.BinopbinopContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#sign}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitSign(GrammarParser.SignContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#function}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitFunction(GrammarParser.FunctionContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#arg}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitArg(GrammarParser.ArgContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#block}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitBlock(GrammarParser.BlockContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitValue(GrammarParser.ValueContext ctx);
}