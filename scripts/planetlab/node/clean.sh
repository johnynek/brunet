#!/bin/bash
# clearing node of any currently running programs....
ps_pid=`ps uax | grep -v "/bin/bash -l" | grep -v "clean.sh" | awk -F" " '{print $2}'`
for i in $ps_pid; do
  sudo kill -KILL $i
done
