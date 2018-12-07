grammar Grammar;

start
    :   program EOF
    ;

program: statement+ SEMICOLON;

statement: binop | assign;
binop: NUM ('+'|'-') (binop|NUM);
assign: 'hello';

NUM: [0-9]+ ;
SEMICOLON: ';' ;
WS : [ \t\n\r] + -> skip
   ;