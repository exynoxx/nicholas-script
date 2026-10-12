use std::collections::HashMap;
fn main() {
    let mut m: HashMap<i64, i64> = HashMap::new();
    let mut x: i64 = 1;
    for _ in 0..5_000_000 {
        x = (x * 1103515245 + 12345) % 2147483648;
        *m.entry(x % 100000).or_insert(0) += 1;
    }
    println!("{} {}", m.len(), m.values().max().unwrap());
}
