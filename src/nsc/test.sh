#!/bin/sh
# Golden tests through the self-hosted compiler: test.sh [nsc binary] [dir...]  (default build/nsc2, ../tests ../examples)
# Programs run in src/ (like `ns test`), stdin from <name>.in when present.
cd "$(dirname "$0")"
here=$(pwd)
nsc=$(realpath "${1:-build/nsc2}"); [ $# -gt 0 ] && shift
dirs=${*:-../tests ../examples}
tmp=$(mktemp -d)
pass=0; fail=0; failed=""
for f in $(ls $(for d in $dirs; do echo $d/*.ns; done)); do
  exp=${f%.ns}.out
  [ -f "$exp" ] || continue
  name=$(basename "$f" .ns)
  inp=$(realpath "${f%.ns}.in" 2>/dev/null); [ -f "$inp" ] || inp=/dev/null
  if "$nsc" "$f" -o "$tmp/$name" >"$tmp/$name.cerr" 2>&1 && (cd .. && "$tmp/$name" <"$inp" >"$tmp/$name.got" 2>&1) && cmp -s "$tmp/$name.got" "$exp"; then
    pass=$((pass+1))
  else
    fail=$((fail+1)); failed="$failed $name"
  fi
done
echo "$pass passed, $fail failed"
[ -n "$failed" ] && echo "failed:$failed" | fold -w 160
rm -rf "$tmp"
[ $fail -eq 0 ]
