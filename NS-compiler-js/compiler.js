var antlr = require('antlr4/index')
var Lexer = require('./GrammarLexer')
var Parser = require('./GrammarParser')
var visitor = require('./GrammarVisitor')

var input = 'var a = 4;'
var chars = new antlr.InputStream(input)
var lexer = new Lexer.GrammarLexer(chars)
var tokens = new antlr.CommonTokenStream(lexer)
var parser = new Parser.GrammarParser(tokens)
var visitor = new visitor.GrammarVisitor()

parser.buildParseTrees = true
var tree = parser.start()
a = 0