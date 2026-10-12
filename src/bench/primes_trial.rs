fn isprime(n: i64) -> bool {
    let mut d = 2;
    while d * d <= n {
        if n % d == 0 { return false; }
        d += 1;
    }
    n > 1
}
fn main() { println!("{}", (2..300_000).filter(|&n| isprime(n)).count()); }
