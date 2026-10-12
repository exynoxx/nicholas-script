n = 10_000_000
s = bytearray([1]) * n
s[0] = s[1] = 0
i = 2
while i * i < n:
    if s[i]:
        s[i * i::i] = bytes(len(range(i * i, n, i)))
    i += 1
p = [k for k in range(n) if s[k]]
print(len(p), sum(p))
