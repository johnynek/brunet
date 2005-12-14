#!/bin/sh

for name in `ls BootGraph*`
do
  ./graph_check.py "$name" | grep miss | wc -l
done
