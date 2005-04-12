#!/usr/bin/env python
# 
#  This ensures that the the deletion statements come after the regular log
#  statements for each node.
#
#
#
import sys, math, re
from sets import Set
from datetime import datetime , timedelta
infilename  = sys.argv[1]

icfile = open( infilename, 'r')  # r for reading
outfilename = infilename + 'reordered'
ofile = open( outfilename, 'w') # w for writing
nodelist = []
#parse times
regexp = re.compile('(\d*)-(\d*)-(\d*)\W(\d*):(\d*):(\d*).(\d*)')

rev_line_list = []
for line in icfile:
  rev_line_list.insert(0,line)

# time --> connection disconnection log statement list
log_dictionary = {}
# node --> deletion time
deletion_time = {}
last_log = {}
one_sec = timedelta(seconds=1)

for line in rev_line_list:
  parsed_line = line.split()
  tmp_date_time = None
  datestring = "%s %s" % (parsed_line[0],parsed_line[1])
  match = regexp.match(datestring)
  if match:
    year = int(match.group(1) )
    month = int( match.group(2) )
    day = int(match.group(3) )
    hour = int( match.group(4) )
    min = int( match.group(5) )
    sec = int( match.group(6) )
    mic = int( match.group(7) )
    tmp_date_time = datetime(year,month,day,hour,min,sec,mic)
  else:
    print "NO MATCH"
  
  if parsed_line[2] == 'deletion' :
    if parsed_line[4] not in deletion_time:
      deletion_time[parsed_line[4]] = None
      print "Added to deletion table", parsed_line[4]
    else:
      pass
      #print "ERROR- multiple deletion", parsed_line[4]
  else:
    tmp_list = []
    if tmp_date_time in log_dictionary:
      tmp_list = log_dictionary[tmp_date_time]
      tmp_list.append(line)
      log_dictionary[tmp_date_time] = tmp_list
    else:
      tmp_list.append(line)
      log_dictionary[tmp_date_time] = tmp_list

    l_a = parsed_line[4] 
    r_a = parsed_line[5] 
    #print parsed_line, l_a , r_a
    if l_a not in last_log:
      last_log[l_a] = tmp_date_time + one_sec
    if r_a not in last_log:
      last_log[r_a] = tmp_date_time + one_sec
      

del_keys = deletion_time.keys()
for dk in del_keys:
  tmp_list = []
  if dk in last_log:
    t_time = last_log[dk]
    deletion_line = "%s deletion deletion %s %s deletion\n" % \
    (t_time.isoformat(' ') ,dk ,dk)
    if t_time in log_dictionary:
      tmp_list = log_dictionary[ t_time ]
      tmp_list.append(deletion_line)
      log_dictionary[t_time] = tmp_list
    else:
      tmp_list.append(deletion_line)
      log_dictionary[t_time] = tmp_list

sorted_keys = log_dictionary.keys()
sorted_keys.sort()
for t_dt in sorted_keys:
  t_l = log_dictionary[t_dt]
  for line in t_l:
    ofile.write(line)

