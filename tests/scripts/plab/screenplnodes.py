#!/usr/bin/env python
# 
# Find nodes that can be shh'ed to and that have ping times under a second.
#
import sys, time, commands, os
from popen2 import *

# This class encapsulates the screening of a list of nodes
class BatchScreen:
  def __init__(self,hostlist,timeout,username,pings):
    self.hostlist = hostlist
    self.node_to_ping_procs = {} 
    self.node_to_ssh_procs = {}
    self.node_to_ping_succ = {}
    self.node_to_ssh_time = {}
    self.timeout = timeout
    self.username = username
    self.cmd_uptime = "uptime"
    self.pings = pings
  #screen the nodes that were provided in the constructor
  # and return a list of successful nodes
  def screen(self):
    for t_host in self.hostlist:
      tmp_host = t_host.strip()
      ping = "ping %s -c %i" % ( tmp_host ,self.pings  )
      ssh = "ssh -o \"ConnectTimeout %i\" -l %s %s %s && echo \"ssh_success\"" % (self.timeout,self.username,tmp_host,self.cmd_uptime)
      ping_t_proc = Popen3(ping)
      ssh_t_proc = Popen3(ssh)
      ping_t_proc.tochild.close()
      ssh_t_proc.tochild.close()
      self.node_to_ping_procs[tmp_host] = ping_t_proc
      self.node_to_ssh_procs[tmp_host] = ssh_t_proc
    time.sleep(self.timeout)
    #get successful ping results
    tmp_int = 0
    for i in self.node_to_ping_procs :
      tmp_int = tmp_int + 1
      tmp = self.node_to_ping_procs[i]
      tmp.wait()
      oput =tmp.poll()
      if oput == 0 :
        self.node_to_ping_succ[i] = "success"
    #get successful ssh results
    tmp_int2 = 0
    for i in self.node_to_ssh_procs :
      tmp_int2 = tmp_int2 + 1
      tmp = self.node_to_ssh_procs[i]
      tmp.wait()
      oput =tmp.fromchild.read().split()
      if len(oput) > 0 :
        if oput[0] != "ssh_success":
          self.node_to_ssh_time[i] = oput[0]
    good_nodes = []
    for i in self.node_to_ping_succ:
      if i in self.node_to_ssh_time:
        good_nodes.append(i) 
    return good_nodes

# the main application

#the list of nodes to screen
infilename  = sys.argv[1]

username = "uclaee_brunet1"
timeout = 15
batchsize = 40
pings = 3
ifile_brunet = open( infilename, 'r')  # r for reading
lines = ifile_brunet.readlines()
num_of_lines = len(lines)
ifile_brunet.close()

begin_slice = 0
end_slice = 0
end_slice = begin_slice + batchsize -1
tmp_list = []
final_nodes = {}
while end_slice < num_of_lines -1 :
  tmp_list = lines[begin_slice:end_slice]
  begin_slice = begin_slice + batchsize
  end_slice = begin_slice + batchsize -1
  tmpbatch = BatchScreen(tmp_list,timeout,username,pings)
  tmp_good_nodes = tmpbatch.screen()
  for gn in tmp_good_nodes:
    final_nodes[gn] = "screened"
if begin_slice < num_of_lines -1 :
  tmp_list = lines[begin_slice:num_of_lines -1]
  tmp_good_nodes = tmpbatch.screen()
  for gn in tmp_good_nodes:
    final_nodes[gn] = "screened"

for fgn in final_nodes:
  print fgn
