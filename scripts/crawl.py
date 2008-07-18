#!/usr/bin/python
""" This crawls the brunet namespace using Information.Info.  During crawling it
also determines if the ring is consistent, does a node agree with its two
neighbors on a given side about their positioning.  This can be used by other
python programs if they call crawl and use the nodes that are returned. """
import xmlrpclib, pybru, sys, getopt, time

usage = """usage:
python crawl.py [--debug] [--debug2] [--port=<xmlrpc port of a brunet node>]
debug = print the current node crawling
debug2 = debug + print the neighbors of current node
port = the xmlrpc port for a brunet node to be used for crawling
help = this message"""

# Default starting point
def main():
  try:
    optlist, args = getopt.getopt(sys.argv[1:], "", ["debug", "port=", "debug2"])

    logger = null_logger
    port = 10000
    debug = False

    for k,v in optlist:
      if k == "--port":
        port = int(v)
      elif k == "--debug":
        logger = print_logger
      elif k == "--debug2":
        logger = print_logger
        debug = True
  except:
    print usage
    return

  nodes = crawl(port, logger, debug)
  count, consistency = check_results(nodes)

  print "Consistent Nodes: " + str(consistency)

  cons = 0
  if consistency != 0:
    cons = consistency / (count * 1.0)
  print "Consistency: " + str(cons)

def check_results(nodes):
  count = 0
  consistency = 0.0

  for addr in nodes:
    count += 1
    consistency += nodes[addr]['consistency']

  return consistency, count

def print_logger(msg):
  print msg

def null_logger(msg):
  pass

# crawls the network using the xmlrpc at the specified port.  Logger is a
# generic function / method thats only parameter is a single string which
# contains some logging output.  This file provides print_logger which calls
# print on the msg and null logger which doesn't do anything.
#
# @todo currently this script does not handle nodes not having left2 and right2
# and nodes will not be skipped until the set of four (right, right2, left,
# left2).  This could make the crawlers results slightly wrong.
def crawl(port = 10000, logger = null_logger, debug = False):
  port = str(port)
  #gain access to the xmlrpc server
  rpc = xmlrpclib.Server("http://127.0.0.1:" + port + "/xm.rem")
  #a list of nodes we have looked up
  nodes = {}

  #getting start node
  node = rpc.localproxy("sys:link.GetNeighbors")['self']
  start = pybru.Address(node)
  last = node
  #maximum times of going back one before failing
  retry_max = 3
  no_response_max = 3

  no_response_count = 0
  retry_count = 0
  #after the transition from node 0->Z, we've visted all nodes less than us
  half_way = False

  while True:
    try:
      logger(node + " " + str(retry_count) + " " + str(no_response_count) + "\n")
      res = rpc.proxy(node, 3, 1, "Information.Info")[0]
      if debug:
        logger(str(res))
      neighbors = res['neighbors']
      info = {}
      info['right'] = neighbors['right']
      info['left'] = neighbors['left']

      try:
        info['right2'] = neighbors['right2']
      except:
        info['right2'] = ""

      try:
        info['left2'] = neighbors['left2']
      except:
        info['left2'] = ""

      ip_list = res['localips']
      ips = ""
      for ip in ip_list:
        ips += ip + ", "
      ips = ips[:-2]
      info['ips'] = ips

      info['geo_loc'] = res['geo_loc']
      info['type'] = res['type']
      if info['type'] == "IpopNode":
        try:
          info['virtual_ip'] = res['Virtual IP']
        except:
          info['virtual_ip'] = ""
        info['namespace'] = res['IpopNamespace']
      info['retries'] = no_response_count * (retry_count + 1)

      no_resonse_count = 0
      if node != last:
        retry_count = 0
    except:
      no_response_count += 1
      if no_response_count == no_response_max:
        node = last
        no_response_count = 0
        retry_count += 1
        if retry_count ==  retry_max:
          print "Unable to crawl the system."
          break
      continue
    #it is possible that the node we're trying to talk to is the one we end up with!
    node = neighbors['self']
    #Once we've visited all nodes less than us, we shouldn't see another until
    #we're done crawling

    if pybru.Address(node) > start:
      half_way = True
    elif half_way and pybru.Address(node) <= start:
      break
    elif pybru.Address(node) == start and len(nodes) > 1:
      half_way = True
      break

    #maintain a list of everyones neighbors
    nodes[node] = info
    last = node
    node = info['right']

  for addr in nodes:
    node = nodes[addr]
    lcons = 0.0
    if(node['left'] in nodes and nodes[node['left']]['right'] == addr):
      lcons += 1.0
    if(node['left2'] in nodes and nodes[node['left2']]['right2'] == addr):
      lcons += 1.0
    if lcons > 0:
      lcons /= 2.0
    nodes[addr]['consistency'] = lcons

  return nodes

if __name__ == "__main__":
  main()
