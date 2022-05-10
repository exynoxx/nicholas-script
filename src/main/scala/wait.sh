inotifywait -q -m -e close_write $1 |
while read -r filename event; do
   g++ $1 && clear  && ./a.out
done
