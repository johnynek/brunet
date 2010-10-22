#!/usr/bin/env python
"""This script makes users utilize deetoo service."""

from pybru import *
import string, time
import sys, random, math, base64
import xmlrpclib, getopt

usage = """usage:
python deetoo.py [--type] [--alpha] [--object][--q_type]
type = cache or query
alpha = replication factor which determines broadcasting range
object = string for caching
q_type = query type ('exact' for exact match or 'regex' for regular expression match)
help = this message"""

def main():
  try:
    optlist, args = getopt.getopt(sys.argv[1:], "", ["type=", "alpha=", "object=", "q_type="])
    print optlist
    for k,v in optlist:
      print k,v
      if k == "--type":
        type = str(v)
      elif k == "--alpha":
        alpha = float(v)
      elif k == "--object":
        try:
          #allow any python code, lists, dicts, etc.. not just strings
          obj = eval(v)
        except:
          obj = str(v)
      elif k == "--q_type":
        q_type = str(v)
      elif k == "--help":
        print usage
  except:
    print usage
    return
  result=0
  if type == "cache":
    result = cache(alpha, obj)
  elif type == "query":
    result = query(alpha, obj, q_type)
  print result
  return result

def cache(alpha, object):
  """replicates object over random bounded range with a given alpha"""
  ht = {}
  ht["task_name"] = "Brunet.Services.Deetoo.MapReduceCache"
  rpc, net_size = setXmlRpcServer()
  print net_size
  rg_start, rg_end = getRange(net_size, alpha)
  ht["gen_arg"] = [rg_start.str, rg_end.str]
  ht["map_arg"] = [object, alpha, rg_start.str, rg_end.str]
  result = rpc.localproxy("mapreduce.Start", ht)
  return result

def query(alpha, object, q_type):
  """send a query over random bounded range.
  @param q_type: query type (exact match or regular expression match"""
  ht = {}
  ht["task_name"] = "Brunet.Services.Deetoo.MapReduceQuery"
  rpc, net_size = setXmlRpcServer(True)
  rg_start, rg_end = getRange(net_size, alpha)
  ht["gen_arg"] = [rg_start.str, rg_end.str]
  ht["map_arg"] = [q_type, object]
  if q_type == "exact":
    ht["reduce_arg"] = ["maxcount", 1]
  else:
    ht["reduce_arg"] = ["concat", True] #second arg is ignored.
  result = rpc.localproxy("mapreduce.Start", ht)
  return result
  

def setXmlRpcServer(query=False):
  """ return random node's url from the list of 
  deetoo installed plab nodes.
  file_name is list of nodes' hostname which installed deetoo
  if query is False, url is for caching node,
  otherwise, url is for querying node.
  """
  url = 0
  net_size = 0
  #port = 9845  #port number for cache node
  port = 10001  #port number for cache node
  svc = "cache"  #name of service 
  if query:
    svc = "query" # name of service
  url = "http://127.0.0.1:" + str(port) + "/" + svc + ".rem"
  print url
  rpc = xmlrpclib.Server(url) # ser xmlrpc server
  net_size = rpc.localproxy("Deetoo.medianestimatedsize")  # estimated network size (StructuredNode.GetSize)
  return rpc, net_size

def getRange(net_size, alpha):
  """returns start and end of range given replication factor"""
  # size of bounded broadcasting range (=\sqrt(\alpha / net_size) * addr_bin_size)
  rg = int(math.sqrt(alpha / float(net_size)) * float(2**160))
  start_addr = 1  #this means nothing, just assigned some number before a loop
  while start_addr %2 != 0: #avoid odd number of address
    start_addr = random.randint(0, 2**160-1)
  end_addr = (start_addr + rg) 
  rg_start = Address(start_addr)
  rg_end = Address(end_addr)   
  print rg_start, rg_end
  return rg_start, rg_end

if __name__ == "__main__":
  main()
