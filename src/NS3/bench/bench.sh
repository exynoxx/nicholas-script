#!/usr/bin/env bash
# Build each bench in NS3 and Rust, run the Python version too, check outputs match Rust,
# print best-of-3 times as a GitHub markdown table. Python runs over 10 s are timed once (marked *).
set -e
NS3=${NS3:-/home/nicholas/Dokumenter/git/nicholas-script/src/NS3/bin/Debug/net9.0/ns3.dll}
export LC_ALL=C; cd "$(dirname "$0")"; T=$(mktemp -d); TIMEFORMAT=%R
best() { local b=999 t; for _ in 1 2 3; do t=$( { time "$@" >/dev/null; } 2>&1 ); b=$(echo "$t $b" | awk '{print ($1<$2)?$1:$2}'); done; echo $b; }
ratio() { awk "BEGIN{printf \"%.1fx\", $1/$2}"; }
echo "| name | rust s | ns3 s | python s | ns3/rust | python/rust |"
echo "|---|--:|--:|--:|--:|--:|"
slow=
for n in primes_sieve primes_trial collatz pipeline hashcount fib dijkstra; do
  dotnet "$NS3" build "$n.ns3" -o "$T/$n.ns3bin" >/dev/null
  rustc -C opt-level=3 -o "$T/$n.rsbin" "$n.rs"
  want=$("$T/$n.rsbin")
  [ "$("$T/$n.ns3bin")" = "$want" ] || echo "$n: NS3 OUTPUT MISMATCH" >&2
  # first python run checks output and gives a timing; only repeat it when it's quick
  p=$( { time python3 "$n.py" >"$T/$n.pyout"; } 2>&1 )
  [ "$(cat "$T/$n.pyout")" = "$want" ] || echo "$n: PYTHON OUTPUT MISMATCH" >&2
  if awk "BEGIN{exit !($p > 10)}"; then
    awk "BEGIN{exit !($p > 60)}" && echo "$n: python took ${p}s" >&2
    p="$p*"; slow=1
  else p=$(best python3 "$n.py"); fi
  r=$(best "$T/$n.rsbin"); s=$(best "$T/$n.ns3bin")
  echo "| $n | $r | $s | $p | $(ratio "$s" "$r") | $(ratio "${p%\*}" "$r") |"
done
if [ -n "$slow" ]; then echo; echo "\\* python over 10 s: single run instead of best of 3"; fi
rm -rf "$T"
