#!/usr/bin/python
import xmlrpclib

# Default starting point
def run(port, nodes, debug):
  port = str(port)
  #gain access to the xmlrpc server
  rpc = xmlrpclib.Server("http://127.0.0.1:" + port + "/xm.rem")
  count = 0

  for node in nodes:
    try:
      rpc.proxy(node, 3, 1, "ncserver.EchoVivaldiState")[0]
      count += 1
    except:
      pass

  return count, len(nodes)
