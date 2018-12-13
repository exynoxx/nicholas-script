grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement: assign|iff|returnn|call;

assign:
VAR ID (COLON TYPE)? EQ eval        #assigneval
| VAR ID (COLON TYPE)? EQ function  #assignfunction
;

iff:
IF LPAREN binop RPAREN block
;

returnn:
RETURN eval
;

call:
ID COLON callarg*;

callarg:
value               #callargvalue
|LPAREN eval RPAREN #callargeval
;


eval:binop|call;

binop:
value                  #binopvalue
| value sign binop     #binopbinop
;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT);

function:
LPAREN (arg (COMMA arg)*)? RPAREN (COLON TYPE)? ARROW block
;
arg:ID COLON TYPE;

block: LBRACKET (statement SEMICOLON)+ RBRACKET;
value:
ID #valueID
| NUM #valueNUM
| STRING #valueSTRING
;


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