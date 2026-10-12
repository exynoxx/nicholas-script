from collections import Counter

m = Counter()
x = 1
for _ in range(5_000_000):
    x = (x * 1103515245 + 12345) % 2147483648
    m[x % 100000] += 1
print(len(m), max(m.values()))
