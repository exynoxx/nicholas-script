var print = function(x) {
 console.log(x) }

var a = "hello"
var b = "world"
var ident = function(x) {
return x
}

var ran1 = "!"
var ran0 = " "
var c = a+ran0+b+ran1
print(c)
print(ident(ident(c)))
var f1 = function(x,y) {
var i = 0
var ret = x
while (i<10) {
ret = ret+y
i = i+1
}
return ret
}

var f2 = function(x,y) {
print(f1(x,y))
}

var ran2 = 1+3
var d = f1(5,ran2)
var ran3 = 1+3
f2(5,ran3)
print(d)
var short = Array.from(new Array(5-1), (x,i) => i + 1)
var long = [1,2,3,4]
print(short)
print(long)
long = Array.from(new Array(2-1), (x,i) => i + 1)
long = short
