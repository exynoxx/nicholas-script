grammar Grammar;

start
    :   program EOF
    ;

program: (statement SEMICOLON)+;

statement:
assign #assignstatement
| ifstatement#ififstatement
| returnn#returnstatement;

assign:
VAR ID EQ binop #assignbinop
| VAR ID EQ function #assignfunction
;
binop: value | value sign binop;
sign: (PLUS|MINUS|MULT|DIV|GE|LE|GT|LT|TILDE);
function: LPAREN (arg (COMMA arg)*)? RPAREN ARROW block;
arg:ID COLON TYPE;
ifstatement: IF LPAREN binop RPAREN block (ELSE block)?;
block: LBRACKET (statement SEMICOLON)+ RBRACKET;
value:SIMPLEVAL #simplevalue
| range #rangevalue
| array #arrayvalue;
range: SIMPLEVAL DOT DOT SIMPLEVAL;
array: LSQUARE (binop (COMMA binop)*)? RSQUARE;
returnn: RETURN binop;
SIMPLEVAL: ID | NUM | STRING;
TYPE: 'int' | 'string';
STRING: '"' ~('"')* '"';
LPAREN: '(';
RPAREN: ')';
QUOTE: '"';
LBRACKET:'{';
RBRACKET:'}';
LSQUARE: '[';
RSQUARE: ']';
PLUS:'+';
MINUS:'-';
DIV:'/';
MULT:'*';
LE:'<=';
GE:'>=';
LT:'<';
GT:'>';
DOT: '.';
TILDE:'~';
ARROW:'=>';
IF: 'if';
ELSE: 'else';
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