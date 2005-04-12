#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a graph
# The current version takes the whole log.
#
import sys, time, copy, stats
from datetime import timedelta, datetime
infilename  = sys.argv[1]

ifile = open( infilename, 'r')  # r for reading

#brunetpingoutname = infilename + "brunet_ping.txt"
#outfile = open(brunetpingoutname, 'w')

# read the data
tmp_address = ""

num_packets = 0
num_bytes = 0

sent_time_to_delta_time = {}
count = 0
needs_to_write = False
uid_to_sent_time = {}
uid_to_received_time = {}
uid_to_all_received_time = {}

nodes_at_t = 0
local_node = ''
remote_node = ''
line = ifile.readline()
tmp_time = 0.0
tmp_status = ''
tmp_uid = 0
delta_list = []
if line:
  parsed_line = line.split()
  if parsed_line[0] == 'local:' :
    local_node = parsed_line[1]
    remote_node = parsed_line[3]
    line = ifile.readline()
    while line:
      parsed_line = line.split()
      if parsed_line[0] != 'local:' :
        delta_list.append( float(parsed_line[1]) )
      else:
        tmp_str = " "
        for number in delta_list:
          tmp_str = tmp_str + str(number) + " "
        print local_node,remote_node,"icmp_ping",tmp_str
        delta_list = []
        if len(parsed_line) > 1:
          local_node = parsed_line[1]
          remote_node = parsed_line[3]
        
      line = ifile.readline()
    
#for time_it in timestamps:
#  if len(datetime_to_bytelist[time_it]) > 1:
#    mtmp = stats.mean(datetime_to_bytelist[time_it])
#    vtmp = stats.var(datetime_to_bytelist[time_it])
#    bytestatsout.write( "%s %f %f %i\n" % (time_it,mtmp,vtmp,len(datetime_to_bytelist[time_it])) )    
#  else :
#    mtmp = stats.mean(datetime_to_bytelist[time_it])
#    bytestatsout.write( "%s %f -1 %i\n" % ( time_it,mtmp,len(datetime_to_bytelist[time_it]) ) )       
