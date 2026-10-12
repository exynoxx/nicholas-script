import heapq

n = 1500
nn = n * n
w = []
x = 1
for _ in range(nn):
    x = (x * 1103515245 + 12345) % 2147483648
    w.append(x // 65536 % 9 + 1)
d = [4_000_000_000_000] * nn
d[0] = 0
q = [(0, 0)]
while q:
    du, u = heapq.heappop(q)
    if du > d[u]:
        continue
    r, c = divmod(u, n)
    nb = []
    if r > 0: nb.append(u - n)
    if r < n - 1: nb.append(u + n)
    if c > 0: nb.append(u - 1)
    if c < n - 1: nb.append(u + 1)
    for v in nb:
        nd = du + w[v]
        if nd < d[v]:
            d[v] = nd
            heapq.heappush(q, (nd, v))
print(d[nn - 1])
