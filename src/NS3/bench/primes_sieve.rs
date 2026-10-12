fn main() {
    let n = 10_000_000usize;
    let mut s = vec![true; n];
    s[0] = false; s[1] = false;
    let mut i = 2;
    while i * i < n {
        if s[i] { (i * i..n).step_by(i).for_each(|j| s[j] = false); }
        i += 1;
    }
    let p: Vec<i64> = (0..n).filter(|&k| s[k]).map(|k| k as i64).collect();
    println!("{} {}", p.len(), p.iter().sum::<i64>());
}
