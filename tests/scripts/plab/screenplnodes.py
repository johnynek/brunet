#!/usr/bin/env python
# 
# This takes a list of nodes and find those nodes 
# that are ping-able, ssh-able and have low time drifts
#
import sys, time, commands, os
from popen2 import *

class BatchScreen:
  def __init__(self,hostlist,timeout,username,pings):
    self.hostlist = hostlist
    self.node_to_ping_procs = {} 
    self.node_to_ssh_procs = {}
    self.node_to_ping_succ = {}
    self.node_to_ssh_time = {}
    self.node_to_localtime = {}
    self.node_to_time_drift = {}
    self.timeout = timeout
    self.username = username
    # self.cmd_uptime = "uptime"
    self.cmd_date = "date +\%s"
    self.pings = pings
  def screen(self):
    #print "The nodes in the batch are:"
    for t_host in self.hostlist:
      tmp_host = t_host.strip()
      #print tmp_host
      ping = "ping %s -c %i" % ( tmp_host ,self.pings  )
      ssh = "timeout %i ssh -o \"ConnectTimeout %i\" -l %s %s %s && echo \"ssh_success\"" % (self.timeout,self.timeout,self.username,tmp_host,self.cmd_date)
      ping_t_proc = Popen3(ping)
      ssh_t_proc = Popen3(ssh)
      ping_t_proc.tochild.close()
      ssh_t_proc.tochild.close()
      f = os.popen('date +\%s')
      self.node_to_localtime[tmp_host] = int(f.readline())
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
    for i in self.node_to_ssh_time :
      self.node_to_time_drift[i] = int(self.node_to_ssh_time[i]) - self.node_to_localtime[i]
    #for i in self.node_to_time_drift :
      # print "%s %i" % (i, self.node_to_time_drift[i])
    #good_nodes = []
    no_ping_nodes = []
    for i in self.node_to_time_drift:
      if i not in self.node_to_ping_succ:
        no_ping_nodes.append(i)
    for i in no_ping_nodes:
      del self.node_to_time_drift[i]
    return self.node_to_time_drift


infilename  = sys.argv[1]
username = "uclaee_brunet1"
timeout = 15
batchsize = 300
pings = 3
drift_thresh = 30 #good nodes must have time drift less than this value
ifile_brunet = open( infilename, 'r')  # r for reading
lines = ifile_brunet.readlines()
num_of_lines = len(lines)
ifile_brunet.close()

begin_slice = 0
end_slice = 0

end_slice = end_slice + batchsize -1
tmp_list = []
final_nodes = {}
node_to_time_drift = {}
tmp_dict = {}
while end_slice < num_of_lines :
  tmp_list = lines[begin_slice:end_slice + 1]
  begin_slice = begin_slice + batchsize
  end_slice = begin_slice + batchsize -1
  tmpbatch = BatchScreen(tmp_list,timeout,username,pings)
  tmp_dict = tmpbatch.screen()
  for gn in tmp_dict.keys():
    node_to_time_drift[gn] = tmp_dict[gn]
if begin_slice < num_of_lines :
  tmp_list = lines[begin_slice:num_of_lines]
  tmpbatch = BatchScreen(tmp_list,timeout,username,pings)
  tmp_dict = tmpbatch.screen()
  for gn in tmp_dict.keys():
    node_to_time_drift[gn] = tmp_dict[gn]

#print "Number of nodes before drift filter: %i" % (len(node_to_time_drift))
for i in node_to_time_drift.keys() :
  if abs(node_to_time_drift[i]) <= drift_thresh :
    print "%s" % (i) #, node_to_time_drift[i])

