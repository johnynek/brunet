#!/usr/bin/env python
# 
# This takes a Brunet connection log creates an RDP record
# The current version takes the whole log.
#
import sys, time, copy, stats, math
from datetime import timedelta, datetime
infilename  = sys.argv[1]
infilename2  = sys.argv[2]

ifile_brunet = open( infilename, 'r')  # r for reading
ifile_icmp = open( infilename2, 'r')  # r for reading

# the bin size in milliseconds
binsize = 5

brunet_address_pair_to_times = {}
icmp_address_pair_to_times = {}

timebin_to_time_list = {}

line = ifile_brunet.readline()
while line:
  parsed_line = line.split()
  tmp_address_pair = parsed_line[0] + " " +  parsed_line[1]
  tmp_len = len(parsed_line)
  tmp_str_list = parsed_line[3:]
  tmp_list = []
  for strnum in tmp_str_list :
    tmp_list.append( float(strnum) )
  brunet_address_pair_to_times[tmp_address_pair] = tmp_list
  line = ifile_brunet.readline()
ifile_brunet.close()

line = ifile_icmp.readline()
while line:
  parsed_line = line.split()
  tmp_address_pair = parsed_line[0] + " " +  parsed_line[1]
  tmp_len = len(parsed_line)
  tmp_str_list = parsed_line[3:]
  tmp_list = []
  for strnum in tmp_str_list :
    tmp_list.append( float(strnum) )
  icmp_address_pair_to_times[tmp_address_pair] = tmp_list
  line = ifile_icmp.readline()
ifile_icmp.close()
  
count = 0  
for add_pair in brunet_address_pair_to_times :
  if add_pair in icmp_address_pair_to_times :
    count = count +1
    icmp_time_list = icmp_address_pair_to_times[add_pair]
    brunet_time_list = brunet_address_pair_to_times[add_pair]
    icmp_len = len(icmp_time_list)
    bn_len = len(brunet_time_list)
    loop_count = bn_len
    rdp_list = []
    rdp_tmp = 0.0
    if icmp_len < bn_len :
      loop_count = icmp_len
    for i in range(loop_count) :
      if brunet_time_list[i] != -1.0 and icmp_time_list[i] != -1.0 :
        if brunet_time_list[i] != 0.0 and icmp_time_list[i] != 0.0 : 
          bin_index = math.floor(icmp_time_list[i]/binsize)*binsize
          rdp_tmp = brunet_time_list[i]/icmp_time_list[i]
          tmp1_list = []
          if bin_index in timebin_to_time_list :
            tmp1_list = timebin_to_time_list[bin_index]
            tmp1_list.append(rdp_tmp)
          else :
            timebin_to_time_list[bin_index] = [rdp_tmp]
          timebin_to_time_list[bin_index] = tmp1_list

#print timebin_to_time_list

bin_final_list = timebin_to_time_list.keys()
bin_final_list.sort()

for bin in bin_final_list :
  if len(timebin_to_time_list[bin]) > 0:
    tmed = stats.lmedian(timebin_to_time_list[bin],100000 )
    tmp_scor_list = timebin_to_time_list[bin]
    tmp_scor_list.sort()
    nin = int( math.floor( 0.9*len(tmp_scor_list) ) )
    tstdband = tmp_scor_list[nin]
    print bin, min(tmp_scor_list),tmed ,tstdband
