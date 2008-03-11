#!/usr/bin/python
""" This crawls the brunet namespace using Information.Info.  During crawling it
also determines if the ring is consistent, does a node agree with its two
neighbors on a given side about their positioning. """
import xmlrpclib, pybru

def main():
  #gain access to the xmlrpc server
  rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")
  #a list of nodes we have looked up
  nodes = {}

  #getting start node
  node = rpc.localproxy("sys:link.GetNeighbors")['self']
  start = pybru.Address(node)
  last = node
  #maximum amount of retries per node before going back one
  no_response_max = 3
  #maximum times of going back one before failing
  retry_max = 3

  no_response_count = 0
  retry_count = 0
  #after the transition from node 0->Z, we've visted all nodes less than us
  half_way = False

  while True:
    try:
      # get information, it throws an exception we try again
      res = rpc.proxy(node, 3, 1, "Information.Info")[0]
      neighbors = res['neighbors']
      info = {}
      info['right'] = neighbors['right']
      info['right2'] = neighbors['right2']
      info['left'] = neighbors['left']
      info['left2'] = neighbors['left2']
    except:
      no_response_count += 1
      if no_response_count == no_response_max:
        node = last
        no_response_count = 0
        retry_count += 1
        if retry_count ==  retry_max:
          print "Unable to crawl the system."
          exit()
      continue
    retry_count = 0
    no_resonse_count = 0

    #Once we've visited all nodes less than us, we shouldn't see another until
    #we're done crawling
    
    if pybru.Address(node) > start:
      half_way = True
    elif half_way and pybru.Address(node) < start:
      break

    #maintain a list of everyones neighbors
    nodes[node] = info
    last = node
    node = info['right']

  count = 0
  consistency = 0.0
  for addr in nodes:
    node = nodes[addr]
    count += 1
    lcons = 0.0
    if(node['left'] in nodes and nodes[node['left']]['right'] == addr):
      lcons += 1.0
    if(node['left2'] in nodes and nodes[node['left2']]['right2'] == addr):
      lcons += 1.0
    if lcons > 0:
      lcons /= 2.0
    consistency += lcons
  print "Total Nodes: " + str(count)
  print "Consistent Nodes: " + str(consistency)
  print "Consistency: " + str(consistency / (count * 1.0))

if __name__ == "__main__":
  main()
