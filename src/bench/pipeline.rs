fn main() {
    let t: i64 = (1..16i64)
        .map(|k| (0..10_000_000i64).filter(|i| i % 3 != 0).map(|i| (i * i + k) % 7).sum::<i64>())
        .sum();
    println!("{}", t);
}
