def isprime(n):
    d = 2
    while d * d <= n:
        if n % d == 0:
            return False
        d += 1
    return n > 1

print(sum(1 for n in range(2, 300_000) if isprime(n)))
