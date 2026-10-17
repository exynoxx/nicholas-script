# Random undirected graph as text: "n m" then m lines "u v" (0-based). Seeded, so reproducible.
import random, sys
n, m = int(sys.argv[1]), int(sys.argv[2])
r = random.Random(42)
print(n, m)
print("\n".join(f"{r.randrange(n)} {r.randrange(n)}" for _ in range(m)))
