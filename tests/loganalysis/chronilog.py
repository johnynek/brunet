#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a graph
# The current version takes the whole log.
#
import sys, time, copy, stats
from datetime import timedelta, datetime
infilename  = sys.argv[1]
mapfilename  = infilename + '.address_map'

ifile = open( infilename, 'r')  # r for reading
mapfile = open( mapfilename, 'w')  # w for writing

time_to_data_list = {}
ah_addresses = []
tmp_local_address = 0
rand_address_to_sequential_int = {}
for line in ifile:
  parsed_line = line.split()
  if parsed_line[0] == 'local_address' :
    tmp_local_address = int(parsed_line[1])
    rand_address_to_sequential_int[tmp_local_address]= 1
  else :
    if len( parsed_line) > 4: 
      tmp_date = parsed_line[0]
      tmp_time = parsed_line[1]
      p_d = tmp_date.split('/')
      p_t = tmp_time.split(':')
      year = int(p_d[2])
      month = int(p_d[0])
      day = int(p_d[1])
      hour = int(p_t[0])
      minute = int(p_t[1])
      second = int(p_t[2])
      microsecond = 1000*int(p_t[3])
      tmp_time = datetime(year,month,day,hour,minute,second,microsecond)
      tmp_data = []
      tmp_data.append(parsed_line[2])
      tmp_data.append(parsed_line[3])
      tmp_data.append(tmp_local_address)
      tmp_data.append( int(parsed_line[4]) )
      
      if tmp_time in time_to_data_list:
        tmp_existing_list = time_to_data_list[tmp_time]
        tmp_existing_list.append(tmp_data)
      else:
        tmp_new_list = []
        tmp_new_list.append(tmp_data)
        time_to_data_list[tmp_time] = tmp_new_list

brunet_addresses = rand_address_to_sequential_int.keys()
brunet_addresses.sort()
tmp_int = 0
for b_add in brunet_addresses:  
  rand_address_to_sequential_int[b_add]= tmp_int
  tmp_int = tmp_int + 1

hash_invert = {}
for b_add in brunet_addresses:
  hash_invert[rand_address_to_sequential_int[b_add] ] = b_add

inverted = hash_invert.keys()
for sm_int in inverted:
  mapfile.write("%i ---> %i\n" % (sm_int,hash_invert[sm_int]) )

timestamps = time_to_data_list.keys()
timestamps.sort()
for time_it in timestamps:
  for tr in time_to_data_list[time_it]:
    print time_it,tr[0],tr[1],rand_address_to_sequential_int[tr[2]],rand_address_to_sequential_int[tr[3]]
   
      

  
