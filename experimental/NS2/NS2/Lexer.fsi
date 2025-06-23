module Lexer

open System
open FSharp.Text.Lexing
open Parser/// Rule tokenize
val tokenize: lexbuf: LexBuffer<char> -> token
