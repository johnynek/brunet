#!/usr/bin/env python
# 
#
import sys, time, commands, os
from popen2 import *
infilename  = sys.argv[1]

timeout = 10
hostname = "hostname"
port =22001
ifile_brunet = open( infilename, 'r')  # r for reading

line = ifile_brunet.readline()
node_to_ping_procs = {}

while line:
  host = line.strip()
  ping = "ping %s -c 5" % ( host)
  ping_t_proc = Popen3(ping,True)
  ping_t_proc.tochild.close()
  ping_t_proc.fromchild.close()
  ping_t_proc.childerr.close()
  node_to_ping_procs[host] = ping_t_proc
  line = ifile_brunet.readline()

time.sleep(timeout)

#get successful ping results

hn_t_proc = Popen3(hostname)
hn_t_proc.tochild.close()
hn_t_proc.wait()
local_hostname_output = hn_t_proc.fromchild.read().split()
local_hostname = ""
if len(local_hostname_output) ==1 :
  local_hostname = local_hostname_output[0]
else:
  print "error"

ofilename = "ping_connections___%s"%(local_hostname)
ofile_brunet = open( ofilename, 'w')  
tmp_str = "%s\n" % (local_hostname)
ofile_brunet.write(tmp_str)

tmp_int = 0
for i in node_to_ping_procs :
  tmp_int = tmp_int + 1
  tmp = node_to_ping_procs[i]
  tmp.wait()
  oput =tmp.poll()
  if oput == 0 :
    tmp_str = "%s\n" % (i)
    ofile_brunet.write(tmp_str)
