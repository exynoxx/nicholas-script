grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement: assign|iff|returnn|call|whilee;

assign:
VAR? ID (COLON TYPE)? EQ eval        #assigneval
| VAR ID (COLON TYPE)? EQ function   #assignfunction
| ID sign EQ binop                   #assigninc
;

iff:
IF LPAREN binop RPAREN block (ELSE block)?
;

whilee: WHILE LPAREN binop RPAREN block
| LOOP block;

returnn:
RETURN eval
;

call:
ID COLON callarg*;

callarg:
value               #callargvalue
|LPAREN eval RPAREN #callargeval
|call               #callargcall
;


eval:binop|call;

binop:
value                  #binopvalue
| value sign binop     #binopbinop
;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT|TILDE|EQUAL);

function:
LPAREN (arg (COMMA arg)*)? RPAREN (COLON TYPE)? ARROW fbody;
fbody: block|iff|eval|assign|whilee;

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
TILDE:'~';
ARROW:'=>';
IF: 'if';
ELSE: 'else';
WHILE: 'while';
LOOP: 'loop';
VAR: 'var' ;
RETURN: 'return';
EQ: '=';
EQUAL: '==';
COMMA: ',';
NUM: [0-9]+ ;
ID: [a-zA-Z0-9]+ ;
SEMICOLON: ';' ;
COLON:':';
COMMENT
: '/*' .*? '*/' -> skip
;
LINE_COMMENT
: '#' ~[\r\n]* -> skip
;
WS : [ \t\n\r] + -> skip
   ;