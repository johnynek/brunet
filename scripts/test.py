#!/usr/bin/python
""" This script executes a set of tests on a Brunet system using the XmlRpc
interface.  Tests must be hard coded into this file to be executed. """
import xmlrpclib, pybru, sys, getopt, time
import crawl, ncservice, map_reduce

usage = """usage:
python test.py [--debug] [--debug2] [--port=<xmlrpc port of a brunet node>]
debug = debugging
port = the xmlrpc port for a brunet node to be used for crawling
help = this message"""

tasks = [ncservice, map_reduce]

# Default starting point
def main():
  try:
    optlist, args = getopt.getopt(sys.argv[1:], "", ["debug", "port="])

    port = 10000
    debug = False

    for k,v in optlist:
      if k == "--port":
        port = int(v)
      elif k == "--debug":
        debug = True
  except:
    print usage
    return

  nodes = crawl.crawl(port, debug = debug) 
  print crawl.check_results(nodes)

  for task in tasks:
    print task.run(port, nodes, debug)

if __name__ == "__main__":
  main()
