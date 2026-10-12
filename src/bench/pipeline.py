print(sum(sum((i * i + k) % 7 for i in range(10_000_000) if i % 3 != 0) for k in range(1, 16)))
