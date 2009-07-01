#!/usr/bin/python
# adds all nodes to one ore more slices
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
 
print "Enter Slice Name(s) - separated by commas: "
slices = raw_input(">")
slices = slices.split(",")

tse = int(time.time()) + 59*60*24*7*8
for slice in slices:
  slice = slice.strip()
  result = api_server.AddSliceToNodes(auth, slice, node_hostnames)
  if result == 1:
    print slice + " Successful"
  else:
    print slice + " Failure"
