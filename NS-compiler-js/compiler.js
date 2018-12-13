var antlr = require('antlr4/index')
var Lexer = require('./GrammarLexer')
var Parser = require('./GrammarParser')
//var grammarvisitor = require('./GrammarVisitor')
var visitor = require('./visitor')

var input = 'a: 4 5;'
var chars = new antlr.InputStream(input)
var lexer = new Lexer.GrammarLexer(chars)
var tokens = new antlr.CommonTokenStream(lexer)
var parser = new Parser.GrammarParser(tokens)
//var visitor = new grammarvisitor.GrammarVisitor()

parser.buildParseTrees = true
var tree = parser.program()
var output = new visitor().visitProgram(tree);
console.log(output)