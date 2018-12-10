grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement:
assign #assignstatement
| ifstatement#ififstatement
| returnn#returnstatement
| call #callstatement;

assign:
VAR ID (COLON TYPE)? EQ binop #assignbinop
| VAR ID (COLON TYPE)? EQ function #assignfunction
;
binop: value | value sign binop;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT);
function: LPAREN (arg (COMMA arg)*)? RPAREN (COLON TYPE)? ARROW block;
arg:ID COLON TYPE;
ifstatement: IF LPAREN binop RPAREN block;
block: LBRACKET (statement SEMICOLON)+ RBRACKET;
value: ID | NUM | STRING;
returnn: RETURN binop;
call: ID COLON (value|LPAREN binop RPAREN)*;

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
RETURN: 'return';
EQ: '=';
COMMA: ',';
NUM: [0-9]+ ;
ID: [a-zA-Z0-9]+ ;
SEMICOLON: ';' ;
COLON:':';
WS : [ \t\n\r] + -> skip
   ;