#!/usr/bin/python
# Returns a list of all planetlab nodes
import xmlrpclib
 
api_server = xmlrpclib.ServerProxy('https://www.planet-lab.org/PLCAPI/', allow_none=True)
 
# Create an empty dictionary (XML-RPC struct)
auth = {"AuthMethod": "anonymous"}
 
all_nodes = api_server.GetNodes(auth, {}, ['hostname'])
 
# Create an array of string hostnames
node_hostnames = [node['hostname'] for node in all_nodes]
f = file("nodes", "w+")
for node in node_hostnames:
  f.write(node + "\n")
