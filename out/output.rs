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
fn range(x:i32)-> Vec<i32> {
let mut tmp:Vec<i32> = vec![].to_vec();
let mut i:i32 = 0;
while (i<x) {
 tmp.push(i); 
i = (i+1);
}
tmp

}
for &mut i in range(10).iter_mut() {
print(&mut toString(i));
}
println(&mut "".to_string());
let mut ran0:Vec<i32> = vec![1,2,3].to_vec();
for &mut i in ran0.iter_mut() {
print(&mut toString(i));
}
println(&mut "".to_string());
}
