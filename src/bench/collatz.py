best, bi = 0, 0
for i in range(1, 3_000_000):
    n, c = i, 1
    while n > 1:
        n = 3 * n + 1 if n % 2 == 1 else n // 2
        c += 1
    if c > best:
        best, bi = c, i
print(bi, best)
