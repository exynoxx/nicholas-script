statement ::= if | while | assign SEMICOLON$
assign ::= def | defshort$
def::=VAR STRING(*) EQ binop|VAR STRING(*) EQ func$
defshort::=STRING(*) DEFEQ binop|STRING(*) DEFEQ func$
binop::=value|value op binop$
op ::= PLUS | MINUS | MULT | DIV | AND | OR | GE | LE | GEQ | LEQ | NEQ$
value ::= INT(*)$