#!/usr/bin/env bash
# Build each bench in NS and Rust, run the Python version too, check outputs match Rust,
# print best-of-3 times as a GitHub markdown table. Python runs over 10 s are timed once (marked *).
set -e
NS=${NS:-$(cd "$(dirname "$0")/.." && pwd)/bin/Release/net10.0/ns.dll}
export LC_ALL=C; cd "$(dirname "$0")"; T=$(mktemp -d); TIMEFORMAT=%R
ulimit -s unlimited  # dfs recurses ~1M deep
[ -f graph.txt ] || python3 gen_graph.py 1000000 1500000 >graph.txt
best() { local b=999 t; for _ in 1 2 3; do t=$( { time "$@" >/dev/null; } 2>&1 ); b=$(echo "$t $b" | awk '{print ($1<$2)?$1:$2}'); done; echo $b; }
ratio() { awk "BEGIN{printf \"%.${3:-1}fx\", $1/$2}"; }
echo "| name | rust s | ns s | python s | slowdown vs rust | speedup vs python |"
echo "|---|--:|--:|--:|--:|--:|"
slow=
for n in primes_sieve primes_trial collatz pipeline hashcount fib dijkstra dfs; do
  dotnet "$NS" build "$n.ns" -o "$T/$n.nsbin" >/dev/null
  rustc -C opt-level=3 -o "$T/$n.rsbin" "$n.rs"
  want=$("$T/$n.rsbin")
  [ "$("$T/$n.nsbin")" = "$want" ] || echo "$n: NS OUTPUT MISMATCH" >&2
  # first python run checks output and gives a timing; only repeat it when it's quick
  p=$( { time python3 "$n.py" >"$T/$n.pyout"; } 2>&1 )
  [ "$(cat "$T/$n.pyout")" = "$want" ] || echo "$n: PYTHON OUTPUT MISMATCH" >&2
  if awk "BEGIN{exit !($p > 10)}"; then
    awk "BEGIN{exit !($p > 60)}" && echo "$n: python took ${p}s" >&2
    p="$p*"; slow=1
  else p=$(best python3 "$n.py"); fi
  r=$(best "$T/$n.rsbin"); s=$(best "$T/$n.nsbin")
  echo "| $n | $r | $s | $p | $(ratio "$s" "$r") | $(ratio "${p%\*}" "$s") |"
done
if [ -n "$slow" ]; then echo; echo "\\* python over 10 s: single run instead of best of 3"; fi
rm -rf "$T"
