#!/usr/bin/env python
# 
# From a list of missing edges in testlog.txt, it 
# finds one node that is to be killed
#
import sys, time, copy, stats
from datetime import timedelta, datetime
infilename  = sys.argv[1]
ifile = open( infilename, 'r')  # are for reading
tmp_local_address = 0
num_freq = {}
for line in ifile:  
  parsed_line = line.split()  
  if parsed_line[0] != 'Missing' :    
    tmp = int(parsed_line[0])
    if tmp not in num_freq:      
      num_freq[tmp]= 1    
    else:      
      num_freq[tmp] = num_freq[tmp] + 1    
    tmp = int(parsed_line[1])    
    if tmp not in num_freq:      
      num_freq[tmp]= 1    
    else:      
      num_freq[tmp] = num_freq[tmp] + 1  
  else :    
    break   
tmp_int = -1
ret_val = -1
test_seq = num_freq.keys()
for b_add in test_seq:    
  if num_freq[b_add] > tmp_int :    
    tmp_int = num_freq[b_add]
    ret_val = b_add
print ret_val
#print num_freq
