#!/bin/bash
instance=$1
PID_FILE="/home/uclaee_brunet1/joe/pid.txt"

pid=' ' 
while read var0 var1; do 
  if [ "$var0" == "$instance" ]; then 
    pid="$var1"; 
    break; 
  fi; 
done < $PID_FILE 
kill -9 $pid
