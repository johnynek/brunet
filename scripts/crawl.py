#!/usr/bin/env python
import xmlrpclib, sys, time, thread, types, getopt, pybru

usage = """usage:
python crawl.py [--debug] [--port=<int>] [--namespace=<string>]
debug = print the current node crawling
port = the xmlrpc port for a brunet node to be used for crawling
namespace = the brunet namespace to crawl
help = this message"""

def main():
  try:
    optlist, args = getopt.getopt(sys.argv[1:], "", ["debug", "port=", "namespace="])

    logger = null_logger
    port = 10000
    ns = ""

    for k,v in optlist:
      if k == "--port":
        port = int(v)
      elif k == "--debug":
        logger = print_logger
      elif k == "--namespace":
        ns = v
  except:
    print usage
    return

  crawler = Crawler(port, logger, ns)
  start = time.time()
  crawler.start()
  crawler.print_results()
  end = time.time()
  print "Crawl took: " + str(end - start)

def print_logger(msg):
  print msg

def null_logger(msg):
  pass

class Crawler:
  def __init__(self, port = 10000, logger = null_logger, ns = ""):
    if not port is types.IntType:
      pass

    self.logger = logger
    port = str(port)
    path = "xm.rem"

    if ns != "":
      #gain access to the xmlrpc server
      rpc = xmlrpclib.Server("http://127.0.0.1:" + port + "/xmserver.rem")
      nodes = rpc.listNodes()
      path = ""
      for node in nodes:
        #All nodes export themselves as namespace:node_address
        if node[:len(ns)] == ns:
          path = node
          break

      if path == "":
        print "Could not find a matching namespace"
        pass

    self.rpc = xmlrpclib.Server("http://127.0.0.1:" + port + "/" + path)
    # access to the "nodes" table
    self.lock = thread.allocate_lock()
    # sort of like a "join" statement
    self.done = thread.allocate_lock()
    self.nodes = {}
    self.attempts = 3

  def start(self):
    node = self.rpc.localproxy("sys:link.GetNeighbors")['self']
    start = pybru.Address(node)
    # we're starting, we'll wait for the last guy to release this lock
    self.done.acquire()
    self.threads = 1
    thread.start_new_thread(self.run, (node,))
    # waiting on the last guy
    self.done.acquire()
    self.consistency()

  def run(self, node):
    attempts = self.attempts

    # query the node and parse the results
    while attempts > 0:
      try:
        res = self.rpc.proxy(node, 3, 1, "Information.Info")
      except:
        # Means the remote side didn't support the operation, empty means no response
        res = {"type": "bad user"}
        try:
          res["neighbors"] = self.rpc.proxy(node, 3, 1, "sys:link.GetNeighbors")[0]
        except:
          res["neighbors"] = {}
        res = [res]
      self.logger(str(res))
      (peers, info) = self.parse(res, self.attempts - attempts)
      if peers != [] and info != {}:
        break
      attempts -= 1
      
    to_query = []

    # add the results to the table and determine any new nodes that need to be queried
    self.lock.acquire()
    if info != {}:
      self.nodes[node] = info
    else:
      self.nodes[node] = self.parse([{"type" : "error"}], self.attempts)[1]
    for neighbor in peers:
      if neighbor in self.nodes:
        continue
      to_query.append(neighbor)
      self.nodes[neighbor] = True

    if to_query == 0:
      # no new nodes, we're done
      self.threads -= 1
    else:
      # add as many new threads as nodes, except the current thread still is working
      self.threads += len(to_query) - 1

    if self.threads == 0:
      # last thread, notifies the main thread of completion
      self.done.release()
    self.logger("Threads remaining: " + str(self.threads))

    self.lock.release()

    # start new threads outside the lock
    for idx in range(len(to_query) - 1):
      thread.start_new_thread(self.run, (to_query[idx], ))

    # this is the thread starter executing
    if len(to_query) > 0:
      self.run(to_query[len(to_query) - 1])

    # when the thread gets here, its completely done
    thread.exit()

  # parse the result of an rpc
  def parse(self, node_info, retries):
    # if no data, return empty sets
    if len(node_info) == 0:
      return ([], {})
    node_info = node_info[0]
    neighbors = node_info['neighbors'] if 'neighbors' in node_info else {}

    peers = []
    for k, v in neighbors.iteritems():
      if k == "self":
        continue
      peers.append(v)

    info = {}
    info['right'] = neighbors['right'] if 'right' in neighbors else ''
    info['right2'] = neighbors['right2'] if 'right2' in neighbors else ''
    info['left'] = neighbors['left'] if 'left' in neighbors else ''
    info['left2'] = neighbors['left2'] if 'left2' in neighbors else ''

    if 'localips' in node_info:
      ip_list = node_info['localips']
      ips = ""
      for ip in ip_list:
        ips += ip + ", "
      ips = ips[:-2]
      info['ips'] = ips
    else:
      info['ips'] = ""

    info['type'] = node_info['type'] if 'type' in node_info else ''
    info['virtual_ip'] = node_info['VirtualIPs'][0] if 'VirtualIPs' in node_info else ''
    info['namespace'] = node_info['IpopNamespace'] if 'IpopNamespace' in node_info else ''
    info['retries'] = retries
    info['cons'] = node_info['cons'] if 'cons' in node_info else 0
    info['tcp'] = node_info['tcp'] if 'tcp' in node_info else 0
    info['tunnel'] = node_info['tunnel'] if 'tunnel' in node_info else 0
    info['udp'] = node_info['udp'] if 'udp' in node_info else 0
    info['subring'] = node_info['subring'] if 'subring' in node_info else 0
    info['sas'] = node_info['sas'] if 'sas' in node_info else 0
    info['wedges'] = node_info['wedges'] if 'wedges' in node_info else 0
    info['geo_loc'] = node_info['geo_loc'] if 'geo_loc' in node_info else ''

    return (peers, info)

  def print_results(self):
    res = self.aggregate()
    print "Total Nodes: " + str(res[0])
    print "Consistent Nodes: " + str(res[1])

    cons = 0
    if res[0] != 0:
      cons = res[1] + res[2] + res[3] + res[4]
      cons = cons / (res[0] * 4.0)
    print "Consistency: " + str(cons)
    print "Connections: " + str(res[6])
    print "SecurityAssociations: " + str(res[7])
    print "WrappedEdges: " + str(res[8])
    print "Edges: " + str(res[5])
    print "RelayEdges: " + str(res[10])
    print "SubringEdges: " + str(res[12])
    print "TcpEdges: " + str(res[9])
    print "UdpEdges: " + str(res[11])

  def consistency(self):
    for addr in self.nodes:
      con = self.consis(addr)
      self.nodes[addr]['consistency'] = (con[0] + con[1] + con[2] + con[3]) / 4.0

  def consis(self, addr):
    node = self.nodes[addr]

    lcon = 1 if(node['right'] in self.nodes and \
        self.nodes[node['right']]['left'] == addr) \
        else 0
    rcon = 1 if(node['left'] in self.nodes and \
        self.nodes[node['left']]['right'] == addr) \
        else 0
    lcon2 = 1 if(node['left2'] in self.nodes and 
        self.nodes[node['left2']]['right2'] == addr) \
        else 0
    rcon2 = 1 if(node['right2'] in self.nodes and \
        self.nodes[node['right2']]['left2'] == addr) \
        else 0

    return (lcon, rcon, lcon2, rcon2)

  def aggregate(self):
    count = 0
    r1consistency = 0
    r2consistency = 0
    l1consistency = 0
    l2consistency = 0
    sas = 0
    cons = 0
    tunnel = 0
    udp = 0
    tcp = 0
    edges = 0
    wedges = 0
    subring = 0
    for addr in self.nodes:
      node = self.nodes[addr]
      count += 1
      consis = self.consis(addr)
      l1consistency += consis[0]
      r1consistency += consis[1]
      l2consistency += consis[2]
      r2consistency += consis[3]
      sas += node['sas']
      cons += node['cons']
      wedges += node['wedges']
      ltunnel = node['tunnel']
      tunnel += ltunnel
      ltcp = node['tcp']
      tcp += ltcp
      ludp = node['udp']
      udp += ludp
      lsubring = node['subring']
      subring += lsubring
      edges += ltunnel + ludp + ltcp + lsubring
      

    return (count, r1consistency, r2consistency, l1consistency, l2consistency, edges, cons, sas, wedges, tcp, tunnel, udp, subring)

if __name__ == "__main__":
  main()
