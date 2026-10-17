// Recursive DFS over graph.txt: component count, largest component, checksum of visit order.
// Recursion goes ~1M deep: run with `ulimit -s unlimited`.
fn dfs(u: usize, adj: &[Vec<usize>], seen: &mut [bool], k: &mut u64, chk: &mut u64) -> usize {
    seen[u] = true;
    *k += 1;
    *chk = (*chk + *k * u as u64) % 1000000007;
    let mut size = 1;
    for &v in &adj[u] {
        if !seen[v] { size += dfs(v, adj, seen, k, chk); }
    }
    size
}

fn main() {
    let t = std::fs::read_to_string("graph.txt").unwrap();
    let a: Vec<usize> = t.split_ascii_whitespace().map(|x| x.parse().unwrap()).collect();
    let (n, m) = (a[0], a[1]);
    let mut adj = vec![Vec::new(); n];
    for e in a[2..2 + 2 * m].chunks(2) {
        adj[e[0]].push(e[1]);
        adj[e[1]].push(e[0]);
    }
    let mut seen = vec![false; n];
    let (mut k, mut chk, mut comps, mut big) = (0, 0, 0, 0);
    for s in 0..n {
        if !seen[s] {
            comps += 1;
            big = big.max(dfs(s, &adj, &mut seen, &mut k, &mut chk));
        }
    }
    println!("{} {} {}", comps, big, chk);
}
