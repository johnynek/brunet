#!/usr/bin/env python
# 
# This takes a Brunet memory log and parses it
# The 10% memory, mean memory, and 90% memory footprint for each
# time bin is output.

# currently the header line for each node needs to be stripped off...or code
# needs to be added to skip lines that are the node headers.

import sys, time, copy, stats, math
from datetime import timedelta, datetime
infilename  = sys.argv[1]

ifile_brunet = open( infilename, 'r')  # r for reading

# the bin size in milliseconds
binsize = 5000.0

time_to_rss_list = {}


line = ifile_brunet.readline()
while line:
  parsed_line = line.split()
  if len(parsed_line) > 1 :
    rss = float(parsed_line[1])
    bin_index = math.floor(float(parsed_line[0])/binsize)*binsize
    if bin_index in time_to_rss_list :
      tmp_list = time_to_rss_list[bin_index]
      tmp_list.append(rss)
      time_to_rss_list[bin_index] =tmp_list
    else :
      tmp_list = [rss]
      time_to_rss_list[bin_index] =tmp_list
  line = ifile_brunet.readline()
      
bin_final_list = time_to_rss_list.keys()
bin_final_list.sort()

for bin in bin_final_list :
  if len(time_to_rss_list[bin]) > 1:
    tmean = stats.mean(time_to_rss_list[bin])
    tmp_scor_list = time_to_rss_list[bin]
    tmp_scor_list.sort()
    nin = int( math.floor( 0.9*len(tmp_scor_list) ) )
    tstdband = tmp_scor_list[nin]
    nin = int( math.floor( 0.1*len(tmp_scor_list) ) )
    tstdlowband = tmp_scor_list[nin]
    
    print bin, tstdlowband,tmean ,tstdband
