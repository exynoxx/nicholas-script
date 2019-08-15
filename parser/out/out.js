//allocNode(ran10,59,null)
var a = 1+1+2+5+3+1
var b = "hek"
var ran4 = "hello"
var ran3 = "hello"
var ran2 = "hello"
var ran1 = "other"
var ran0 = "world"
var y = ran0+ran1+b+ran2+ran3+ran4
var f1 = function(x) {
//allocNode(ran7,0,null)
var ran5 = x+x+2*x
//freeNode(ran7,null)
return ran5
}
var f2 = function(i) {
//allocNode(ran8,8,null)
var t = "hell"
var triple = i+i+i
var gg = "gwrg"
//freeNode(ran8,null)
return 0
}
var c = f1(1)

var ran6 = "hey"
var d = f2(ran6)

var print = function(x) {
//allocNode(ran9,0,null)
 console.log(x) //freeNode(ran9,null)
}
print(c)
print(d)
print(a)
print(b)
print(y)
//freeNode(ran10,null)
