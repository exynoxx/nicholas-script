grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement:
  assign        #assignstatement
| iff           #ifstatement
| returnn       #returnstatement
| call          #callstatement
;

assign:
VAR ID (COLON TYPE)? EQ eval        #assigneval
| VAR ID (COLON TYPE)? EQ functionn  #assignfunction
;

iff:
IF LPAREN binop RPAREN block
;

returnn:
RETURN eval
;

call:
ID COLON (value|LPAREN eval RPAREN)*
;

eval:
binop   #evalbinop
|call   #evalcall
;

binop:
value                  #binopvalue
| value sign binop     #binopbinop
;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT);

functionn:
LPAREN (arg (COMMA arg)*)? RPAREN (COLON TYPE)? ARROW block
;
arg:ID COLON TYPE;

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
RETURN: 'return';
EQ: '=';
COMMA: ',';
NUM: [0-9]+ ;
ID: [a-zA-Z0-9]+ ;
SEMICOLON: ';' ;
COLON:':';
WS : [ \t\n\r] + -> skip
   ;