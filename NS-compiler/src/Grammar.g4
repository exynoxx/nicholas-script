grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement: assign | ifstatement;
binop: NUM | NUM (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT) binop;
assign: VAR id=ID EQ assignee=binop;
ifstatement: IF LPAREN cond=binop RPAREN body=block;
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