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
	 * Visit a parse tree produced by the {@code assignstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitAssignstatement(GrammarParser.AssignstatementContext ctx);
	/**
	 * Visit a parse tree produced by the {@code ififstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitIfifstatement(GrammarParser.IfifstatementContext ctx);
	/**
	 * Visit a parse tree produced by the {@code returnstatement}
	 * labeled alternative in {@link GrammarParser#statement}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitReturnstatement(GrammarParser.ReturnstatementContext ctx);
	/**
	 * Visit a parse tree produced by the {@code assignbinop}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitAssignbinop(GrammarParser.AssignbinopContext ctx);
	/**
	 * Visit a parse tree produced by the {@code assignfunction}
	 * labeled alternative in {@link GrammarParser#assign}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitAssignfunction(GrammarParser.AssignfunctionContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#binop}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitBinop(GrammarParser.BinopContext ctx);
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
	 * Visit a parse tree produced by {@link GrammarParser#ifstatement}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitIfstatement(GrammarParser.IfstatementContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#block}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitBlock(GrammarParser.BlockContext ctx);
	/**
	 * Visit a parse tree produced by the {@code simplevalue}
	 * labeled alternative in {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitSimplevalue(GrammarParser.SimplevalueContext ctx);
	/**
	 * Visit a parse tree produced by the {@code rangevalue}
	 * labeled alternative in {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitRangevalue(GrammarParser.RangevalueContext ctx);
	/**
	 * Visit a parse tree produced by the {@code arrayvalue}
	 * labeled alternative in {@link GrammarParser#value}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitArrayvalue(GrammarParser.ArrayvalueContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#range}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitRange(GrammarParser.RangeContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#array}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitArray(GrammarParser.ArrayContext ctx);
	/**
	 * Visit a parse tree produced by {@link GrammarParser#returnn}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitReturnn(GrammarParser.ReturnnContext ctx);
}