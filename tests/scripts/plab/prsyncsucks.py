#!/usr/bin/env python
# 
# This takes a list of nodes and find those nodes 
# that are ping-able, ssh-able and have low time drifts
#
import sys, time, commands, os
from popen2 import *

class BatchRsyncSuck:
  def __init__(self,hostlist,timeout,username,remotepath,localpath):
    self.hostlist = hostlist
    self.node_to_rsync_procs = {}
    self.node_to_successful_rsync = {}
    self.node_to_unsuccessful_rsync = {}
    self.timeout = timeout
    self.username = username
    self.remotepath = remotepath
    self.localpath = localpath
  def rsync_suck(self):
    #print "The nodes in the batch are:"
    for t_host in self.hostlist:
      tmp_host = t_host.strip()
      #print tmp_host
      rsync = "timeout %i rsync -rz %s@%s:%s %s/%s" % (3*self.timeout,self.username,tmp_host,self.remotepath,self.localpath,tmp_host)
      rsync_t_proc = Popen3(rsync)
      rsync_t_proc.tochild.close()
      self.node_to_rsync_procs[tmp_host] = rsync_t_proc
    time.sleep(self.timeout) 
    #get successful ssh results
    tmp_int2 = 0
    for i in self.node_to_rsync_procs :
      tmp_int2 = tmp_int2 + 1
      tmp = self.node_to_rsync_procs[i]
      tmp.wait()
      ret_code = tmp.poll()
      if ret_code == 0:
        self.node_to_successful_rsync[i] = tmp
      else:
        self.node_to_unsuccessful_rsync[i] = tmp
    return self.node_to_unsuccessful_rsync


infilename  = sys.argv[1]
username = "uclaee_brunet1"
remotepath = "/home/uclaee_brunet1/joe/data/"
localpath = "/home/jskong/results/data/"
#username = "jsab"
#remotepath = "/home/jsab/testrsync/"
#localpath = "/home/jsab/testrsync"
timeout = 15
batchsize = 100
pings = 3
ifile_brunet = open( infilename, 'r')  # r for reading
lines = ifile_brunet.readlines()
num_of_lines = len(lines)
ifile_brunet.close()

begin_slice = 0
end_slice = 0

end_slice = end_slice + batchsize -1
tmp_list = []
final_nodes = {}
tmp_dict = {}
while end_slice < num_of_lines :
  tmp_list = lines[begin_slice:end_slice + 1]
  begin_slice = begin_slice + batchsize
  end_slice = begin_slice + batchsize -1
  tmpbatch = BatchRsyncSuck(tmp_list,timeout,username,remotepath,localpath)
  tmp_dict = tmpbatch.rsync_suck()
  for gn in tmp_dict.keys():
    final_nodes[gn] = tmp_dict[gn]
if begin_slice < num_of_lines :
  tmp_list = lines[begin_slice:num_of_lines]
  tmpbatch = BatchRsyncSuck(tmp_list,timeout,username,remotepath,localpath)
  tmp_dict = tmpbatch.rsync_suck()
  for gn in tmp_dict.keys():
    final_nodes[gn] = tmp_dict[gn]

#print "Number of nodes before drift filter: %i" % (len(node_to_time_drift))
for i in final_nodes.keys() :
  #print "Node: %s Error Code: %i" % (i,final_nodes[i].poll()) #, node_to_time_drift[i])
  print "Error on %s" % (i)
