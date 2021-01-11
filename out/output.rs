#![allow(unused_parens)]
#![allow(unused_mut)]
#![allow(non_snake_case)]
fn main(){
fn print(x:&mut String) {
 print!("{} ",x); 

}
fn println(x:&mut String) {
 println!("{}",x); 

}
fn toString(x:i32)-> String {
 return x.to_string(); 

}
fn toInt(x:&mut String)-> i32 {
 return x.parse::<i32>().unwrap(); 

}
fn printArr(x:&mut Vec<i32>) {
for &mut e in x.iter_mut() {
print(&mut toString(e));
}
println(&mut "".to_string());

}
fn map(f:fn(i32)->i32,l:&mut Vec<i32>)-> Vec<i32> {
let mut tmp:Vec<i32> = vec![].to_vec();
for &mut e in l.iter_mut() {
let mut y:i32 = f(e);
 tmp.push (y); 
}
tmp

}
}
