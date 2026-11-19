#!/bin/bash
mkdir -p out
# id boxes tries maxPushes budget
while read id b t mp bud tg mode; do
  [ -z "$id" ] && continue
  dotnet bin/Debug/net10.0/LevelLab.dll gen tpl/$id.txt $b $t 7 $mp $bud $tg $mode > out/$id.txt 2>&1
  echo "$id: $(head -1 out/$id.txt)"
done <<LIST
01 1 300 6 20000 7
02 2 2000 12 20000 20
03 2 3000 30 40000 30
04 4 3000 40 60000 40
05 1 2000 30 40000 25
06 2 3000 40 40000 32
07 4 3000 40 60000 50
08 3 3000 60 60000 48
09 1 1000 20 20000 12
10 2 3000 40 40000 35
11 4 3000 40 60000 55
12 4 3000 40 60000 60
13 1 2000 30 40000 16
14 2 3000 40 60000 45
15 2 3000 40 60000 55
16 3 3000 50 60000 65
17 3 3000 50 60000 72
18 3 3000 50 60000 76
19 2 3000 50 60000 80
20 3 3000 50 60000 86 any
LIST
