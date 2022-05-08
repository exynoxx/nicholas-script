inotifywait -q -m -e close_write $1 |
while read -r filename event; do
  clear && g++ $1 && ./a.out
done
