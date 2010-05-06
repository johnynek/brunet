#!/usr/bin/python
import sys, xmlrpclib, os, signal, time

def run(port, nodes, debug):
  port = str(port)
  #gain access to the xmlrpc server
  #rpc = xmlrpclib.Server("http://127.0.0.1:" + port + "/xm.rem")
  rpc = xmlrpclib.Server("http://ds-pl3.technion.ac.il:" + port + "/cache.rem")
  res = 0
  left = len(nodes) - 1

  node = rpc.localproxy("sys:link.GetNeighbors")['self']

  while left > 0:
    node = nodes[node]['right']
    try:
      ht = {}
      ht["task_name"] = "Brunet.MapReduceRangeCounter"
      ht["gen_arg"] = node
      result = rpc.localproxy("mapreduce.Start", ht)
      count = result["count"]
      if count == left:
        res += 1
      left -= 1
    except "x":
      pass

  return res, len(nodes) - 1
