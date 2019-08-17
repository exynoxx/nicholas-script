//allocNode(ran4,5,null)
var print = function(x) {
//allocNode(ran1,0,null)
 console.log(x) //freeNode(ran1,null)
}
var t = "hello"
var ident = function(x) {
//allocNode(ran2,0,null)
//freeNode(ran2,null)
return x
}
var add = function(x,y) {
//allocNode(ran3,0,null)
var ran0 = x+y
//freeNode(ran3,null)
return ran0
}
var a = ident(t)
ident(t)
var b = ident(ident(ident(t)))
ident(ident(ident(t)))
var c = add(add(add(1,2),2),2)
//freeNode(ran4,null)
