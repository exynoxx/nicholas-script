fn main(){
fn print(x:String) {
 println!("{}",x); 
}
fn a()-> i32 {
let mut ran0:i32 = (1+1);
ran0
}
fn concat(s1:String,s2:String)-> String {
let mut ran1:String = (s1+s2);
ran1
}
fn poly(x:i32)-> i32 {
let mut ran2:i32 = (5*x.pow(3)+3*x.pow(2)+1*x);
ran2
}
let mut b:i32 = a();
print(concat(poly(5),poly(b)));
let mut ran3:i32 = (1+1);
let mut ran4:i32 = (2*2);
let mut ran5:i32 = (3%3);
print(ran3,ran4,ran5);
fn toInt(x:String) {
 return x.parse::<i32>().unwrap(); 
}
let mut vb:i32 = 0;
fn toString(x:i32) {
 return x.to_string(); 
}
print(toInt("500"));
print(toInt("600"));
print(toInt("700"));
print(toString((1+5*4*(102+1))));
}
