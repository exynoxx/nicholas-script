fn main() {
    let (mut best, mut bi) = (0, 0);
    for i in 1..3_000_000i64 {
        let (mut n, mut c) = (i, 1);
        while n > 1 {
            n = if n % 2 == 1 { 3 * n + 1 } else { n / 2 };
            c += 1;
        }
        if c > best { best = c; bi = i; }
    }
    println!("{} {}", bi, best);
}
