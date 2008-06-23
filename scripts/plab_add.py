#!/usr/bin/python
import xmlrpclib
 
api_server = xmlrpclib.ServerProxy('https://www.planet-lab.org/PLCAPI/', allow_none=True)
 
# Create an empty dictionary (XML-RPC struct)
auth = {}
 
# Specify password authentication
auth['AuthMethod'] = 'password'
 
print "Enter Username: "
username = raw_input(">")
print "Enter Password: "
password = raw_input(">")
# Username and password
auth['Username'] = username
auth['AuthString'] = password
 
# Checks to see if we are authorized
authorized = api_server.AuthCheck(auth)
if authorized:
  print 'We are authorized!'
 
# This may take a while.
all_nodes = api_server.GetNodes(auth, {}, ['hostname'])
 
# Create an array of string hostnames
node_hostnames = [node['hostname'] for node in all_nodes]
print node_hostnames
 
print "Enter Slice Name: "
slice = raw_input(">")
# Add slice to each node
result = api_server.AddSliceToNodes(auth, slice, node_hostnames)

