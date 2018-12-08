grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement:assign #assignstatement
| ifstatement#ififstatement;

assign:
VAR ID EQ binop #assignbinop
| VAR ID EQ function #assignfunction
;
binop: value | value sign binop;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT);
function: LPAREN (arg (COMMA arg)*)? RPAREN ARROW block;
arg:ID COLON TYPE;
ifstatement: IF LPAREN binop RPAREN block;
block: LBRACKET (statement SEMICOLON)+ RBRACKET;
value: ID | NUM | STRING;
TYPE: 'int' | 'string';
STRING: '"' ~('"')* '"';
LPAREN: '(';
RPAREN: ')';
QUOTE: '"';
LBRACKET:'{';
RBRACKET:'}';
PLUS:'+';
MINUS:'-';
DIV:'/';
MULT:'*';
LE:'<=';
GE:'>=';
LT:'<';
GT:'>';
ARROW:'=>';
IF: 'if';
VAR: 'var' ;
EQ: '=';
COMMA: ',';
NUM: [0-9]+ ;
ID: [a-zA-Z0-9]+ ;
SEMICOLON: ';' ;
COLON:':';
WS : [ \t\n\r] + -> skip
   ;