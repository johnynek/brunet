#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a graph
# The current version takes the whole log.
#
import sys
infilename  = sys.argv[1]

ifile = open( infilename, 'r')  # r for reading

total_packets = 0
needs_to_write = False
last_name = ''

for line in ifile:
  parsed_line = line.split()
  if parsed_line[0] == 'Local_node:' :
    if False == needs_to_write :
      pass
    else:  
      print last_name,total_packets
    total_packets = 0
    if len(parsed_line) > 1 :
      last_name = parsed_line[1]
  else:
    needs_to_write = True
    total_packets = total_packets + 1
