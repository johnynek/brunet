#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it
# The current version takes the whole log.
#
import sys, time, copy, stats
from datetime import timedelta, datetime
infilename  = sys.argv[1]

deltatime = timedelta( milliseconds = float(sys.argv[2]) )
direction = sys.argv[3] # this should be "sent, received, total"

ifile = open( infilename, 'r')  # r for reading

packetstatsname = infilename + "packet_output.txt"
packetstatsout = open(packetstatsname, 'w')
bytestatsname = infilename + "bytes_stats_output.txt"
bytestatsout = open(bytestatsname, 'w')

# read the data
tmp_address = ""
start_time = datetime.today()
outputtime = datetime.today()

num_packets = 0
num_bytes = 0

deltaincr = copy.deepcopy(deltatime)
tmpdeltamultiple = 1
datetime_to_packetlist = {}
datetime_to_bytelist = {}

count = 0
needs_to_write = False

nodes_at_t = 0

for line in ifile:
  #count = count +1
  #print count
  parsed_line = line.split()
  if parsed_line[0] == 'Local_node:' :
    if needs_to_write == True:
      outputtime = start_time + deltaincr*tmpdeltamultiple
      #print outputtime.ctime()
      #print "-----"
      
      if outputtime in datetime_to_packetlist:
        t_list = datetime_to_packetlist[outputtime]
        t_list.append(num_packets)
        datetime_to_packetlist[outputtime] = t_list
      else :
        datetime_to_packetlist[outputtime] = [num_packets]
      
      if outputtime in datetime_to_bytelist:
        t_list = datetime_to_bytelist[outputtime]
        t_list.append(num_bytes)
        datetime_to_bytelist[outputtime] = t_list
      else :
        datetime_to_bytelist[outputtime] = [num_bytes]
      num_packets = 0 
      num_bytes = 0
    
    tmpdeltamultiple = 1
    #print  parsed_line[0] , parsed_line[1] , parsed_line[3] ,parsed_line[4]
     
    if len( parsed_line) > 1: 
      num_packets = 0
      num_bytes = 0
      tmp_address = parsed_line[1]    
      tmp_date = parsed_line[3]
      tmp_time = parsed_line[4]
      p_d = tmp_date.split('/')
      p_t = tmp_time.split(':')
      year = int(p_d[2])
      month = int(p_d[0])
      day = int(p_d[1])
      hour = int(p_t[0])
      minute = int(p_t[1])
      second = int(p_t[2])
      start_time = datetime(year,month,day,hour,minute,second)
      deltaincr = copy.deepcopy(deltatime)
  else:
    c_f = float(parsed_line[0])/1000.0
    packetdelta =  timedelta( seconds = c_f )
    
    packetbytes = int(parsed_line[1])
    needs_to_write = True
    outputtime = start_time + deltaincr*tmpdeltamultiple
    #print  outputtime.ctime()
    if deltaincr*tmpdeltamultiple > packetdelta :
      if direction == 'total' :
        num_packets = num_packets + 1
        num_bytes = num_bytes + packetbytes
      elif parsed_line[3] == direction:
        num_packets = num_packets + 1
        num_bytes = num_bytes + packetbytes
      else:
        print "ERROR in line parsing"
    else :
      needs_to_write = False
      outputtime = start_time + deltaincr*tmpdeltamultiple
      
      if outputtime in datetime_to_packetlist:
        t_list = datetime_to_packetlist[outputtime]
        t_list.append(num_packets)
        datetime_to_packetlist[outputtime] = t_list
      else :
        datetime_to_packetlist[outputtime] = [num_packets]
      
      if outputtime in datetime_to_bytelist:
        t_list = datetime_to_bytelist[outputtime]
        t_list.append(num_bytes)
        datetime_to_bytelist[outputtime] = t_list
      else :
        datetime_to_bytelist[outputtime] = [num_bytes]
      num_packets = 0 
      num_bytes = 0
      tmpdeltamultiple = tmpdeltamultiple + 1
 
timestamps = datetime_to_packetlist.keys()
timestamps.sort()
for time_it in timestamps:
  if len(datetime_to_packetlist[time_it]) > 1:
    mtmp = stats.mean(datetime_to_packetlist[time_it])
    vtmp = stats.var(datetime_to_packetlist[time_it])
    packetstatsout.write( "%s %f %f %i\n" % (time_it,mtmp,vtmp,len(datetime_to_packetlist[time_it])) )    
  else :
    mtmp = stats.mean(datetime_to_packetlist[time_it])
    packetstatsout.write( "%s %f -1 %i\n" % (time_it,mtmp,len(datetime_to_packetlist[time_it])) )    
 
  #datetime_to_packetlist[time_it]

timestamps = datetime_to_bytelist.keys()
timestamps.sort()
for time_it in timestamps:
  if len(datetime_to_bytelist[time_it]) > 1:
    mtmp = stats.mean(datetime_to_bytelist[time_it])
    vtmp = stats.var(datetime_to_bytelist[time_it])
    bytestatsout.write( "%s %f %f %i\n" % (time_it,mtmp,vtmp,len(datetime_to_bytelist[time_it])) )    
  else :
    mtmp = stats.mean(datetime_to_bytelist[time_it])
    bytestatsout.write( "%s %f -1 %i\n" % ( time_it,mtmp,len(datetime_to_bytelist[time_it]) ) )    
    
    
