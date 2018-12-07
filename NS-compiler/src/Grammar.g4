grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement: assign | ifstatement;
binop: NUM | NUM (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT) binop;
assign: VAR ID EQ binop;
ifstatement: IF LPAREN binop RPAREN block;
block: LBRACKET program RBRACKET;

LPAREN: '(';
RPAREN: ')';
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
IF: 'if';
VAR: 'var' ;
EQ: '=';
NUM: [0-9]+ ;
ID: [a-zA-Z]+ ;
SEMICOLON: ';' ;
WS : [ \t\n\r] + -> skip
   ;