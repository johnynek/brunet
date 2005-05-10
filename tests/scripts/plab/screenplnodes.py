#!/usr/bin/env python
# 
# This takes a list of nodes and find those nodes 
# that are ping-able, ssh-able and have low time drifts
#
import sys, time, commands, os, re
from popen2 import *

class BatchScreen:
  def __init__(self,hostlist,timeout,username,pings,drift_thresh,load_thresh):
    self.hostlist = hostlist
    self.node_to_ping_procs = {} 
    self.node_to_ssh_procs = {}
    self.node_to_ssh_uptime_procs = {}
    self.node_to_ping_succ = {}
    self.node_to_ssh_time = {}
    self.node_to_localtime = {}
    self.node_to_time_drift = {}
    self.node_to_load = {}
    self.timeout = timeout
    self.username = username
    self.cmd_uptime = "uptime"
    self.cmd_date = "date +%s"
    self.pings = pings
    self.drift_thresh = drift_thresh
    self.load_thresh = load_thresh
  def screen(self):
    for t_host in self.hostlist:
      tmp_host = t_host.strip()
      ping = "ping %s -c %i" % ( tmp_host ,self.pings  )
      ssh = "timeout %i ssh -o \"ConnectTimeout %i\" -l %s %s \"%s\" && echo \"ssh_success\"" % \
      (self.timeout,self.timeout,self.username,tmp_host,self.cmd_date)
      ssh_uptime = "timeout %i ssh -o \"ConnectTimeout %i\" -l %s %s \"%s\" && echo \"ssh_success\"" % \
      (self.timeout,self.timeout,self.username,tmp_host,self.cmd_uptime)
      ping_t_proc = Popen3(ping)
      ssh_t_proc = Popen3(ssh)
      ssh_uptime_t_proc = Popen3(ssh_uptime)
      ping_t_proc.tochild.close()
      ssh_t_proc.tochild.close()
      ssh_uptime_t_proc.tochild.close() 
      f = os.popen('date +\%s')
      self.node_to_localtime[tmp_host] = int(f.readline())
      f.close()
      self.node_to_ping_procs[tmp_host] = ping_t_proc
      self.node_to_ssh_procs[tmp_host] = ssh_t_proc
      self.node_to_ssh_uptime_procs[tmp_host] = ssh_uptime_t_proc
    time.sleep(5) 
    #get successful ping results
    tmp_int = 0
    for i in self.node_to_ping_procs :
      tmp_int = tmp_int + 1
      tmp = self.node_to_ping_procs[i]
      tmp.wait()
      oput =tmp.poll()
      if oput == 0 :
        self.node_to_ping_succ[i] = "success"
    #get successful ssh results (for system's current time)
    tmp_int2 = 0
    for i in self.node_to_ssh_procs :
      tmp_int2 = tmp_int2 + 1
      tmp = self.node_to_ssh_procs[i]
      tmp.wait()
      oput =tmp.fromchild.read().split()
      if len(oput) > 0 :
        if oput[0] != "ssh_success":
          self.node_to_ssh_time[i] = oput[0]
    #get successful ssh results (for system's load)
    p = re.compile('load average:\s*(\d+\.\d*),\s*(\d+\.\d*),\s*(\d+\.\d*)') 
    tmp_int3 = 0
    for i in self.node_to_ssh_uptime_procs :
      tmp_int3 = tmp_int3 + 1
      tmp = self.node_to_ssh_uptime_procs[i]
      tmp.wait()
      tmp_str =tmp.fromchild.read()
      oput = tmp_str.split()
      if len(oput) > 0 :
        if oput[0] != "ssh_success":
	  five_min_load = float( p.search(tmp_str).group(2) )
          self.node_to_load[i] = five_min_load

    for i in self.node_to_ssh_time.keys() :
      self.node_to_time_drift[i] = int(self.node_to_ssh_time[i]) - self.node_to_localtime[i]
    #for i in self.node_to_time_drift :
      # print "%s %i" % (i, self.node_to_time_drift[i])
    good_nodes = []
    #no_ping_nodes = []
    for i in self.node_to_time_drift:
      if (i in self.node_to_ping_succ) and (abs(self.node_to_time_drift[i]) <= self.drift_thresh) and (i in self.node_to_load) and (self.node_to_load[i] < self.load_thresh):
        good_nodes.append(i)
    return good_nodes

infilename  = sys.argv[1]
username = "uclaee_brunet1"
timeout = 15
batchsize = 200
pings = 3
drift_thresh = 30 #good plab machines must have time drift less than this value in seconds
load_thresh = 10.0 #good plab machines must have a load value less than this
ifile_brunet = open( infilename, 'r')  # r for reading
lines = ifile_brunet.readlines()
num_of_lines = len(lines)
ifile_brunet.close()

begin_slice = 0
end_slice = 0

end_slice = end_slice + batchsize -1
tmp_list = []
final_nodes = []
tmp_nodes = []
while end_slice < num_of_lines :
  tmp_list = lines[begin_slice:end_slice + 1]
  begin_slice = begin_slice + batchsize
  end_slice = begin_slice + batchsize -1
  tmpbatch = BatchScreen(tmp_list,timeout,username,pings,drift_thresh,load_thresh)
  tmp_nodes = tmpbatch.screen()
  for node in tmp_nodes :
    final_nodes.append(node)
if begin_slice < num_of_lines :
  tmp_list = lines[begin_slice:num_of_lines]
  tmpbatch = BatchScreen(tmp_list,timeout,username,pings,drift_thresh,load_thresh)
  tmp_nodes = tmpbatch.screen()
  for node in tmp_nodes :
    final_nodes.append(node)

for i in final_nodes :
  print "%s" % (i) 

