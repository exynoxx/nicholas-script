use std::cmp::Reverse;
use std::collections::BinaryHeap;
fn main() {
    let n = 1500usize;
    let nn = n * n;
    let mut x: i64 = 1;
    let w: Vec<i64> = (0..nn).map(|_| { x = (x * 1103515245 + 12345) % 2147483648; x / 65536 % 9 + 1 }).collect();
    let mut d = vec![4_000_000_000_000i64; nn];
    d[0] = 0;
    let mut q = BinaryHeap::new();
    q.push(Reverse((0i64, 0usize)));
    while let Some(Reverse((du, u))) = q.pop() {
        if du > d[u] { continue; }
        let (r, c) = (u / n, u % n);
        let mut nb = Vec::with_capacity(4);
        if r > 0 { nb.push(u - n); }
        if r < n - 1 { nb.push(u + n); }
        if c > 0 { nb.push(u - 1); }
        if c < n - 1 { nb.push(u + 1); }
        for v in nb {
            let nd = du + w[v];
            if nd < d[v] { d[v] = nd; q.push(Reverse((nd, v))); }
        }
    }
    println!("{}", d[nn - 1]);
}
