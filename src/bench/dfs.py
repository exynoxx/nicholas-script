# Recursive DFS over graph.txt: component count, largest component, checksum of visit order.
import sys
sys.setrecursionlimit(10**7)
a = list(map(int, open("graph.txt").read().split()))
n, m = a[0], a[1]
adj = [[] for _ in range(n)]
for i in range(2, 2 + 2 * m, 2):
    u, v = a[i], a[i + 1]
    adj[u].append(v); adj[v].append(u)
seen = [False] * n
k = chk = size = 0
def dfs(u):
    global k, chk, size
    seen[u] = True; size += 1; k += 1
    chk = (chk + k * u) % 1000000007
    for v in adj[u]:
        if not seen[v]: dfs(v)
comps = big = 0
for s in range(n):
    if not seen[s]:
        comps += 1; size = 0; dfs(s); big = max(big, size)
print(comps, big, chk)
