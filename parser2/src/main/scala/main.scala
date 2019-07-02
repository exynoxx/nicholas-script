object HelloWorld extends App {
    val l = new lexer;
  print(l.tokenize("while (  a >= 1 || b < 0) {}"))
}

