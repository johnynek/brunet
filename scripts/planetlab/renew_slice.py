#!/usr/bin/python
import xmlrpclib, sys, time
 
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
 
authorized = False
try:
# Checks to see if we are authorized
  authorized = api_server.AuthCheck(auth)
except:
  sys.exit()
if authorized:
  print 'We are authorized!'
 
print "Enter Slice Name: "
slice = raw_input(">")

tse = int(time.time()) + 59*60*24*7*8
result = api_server.UpdateSlice(auth, slice, {"expires": tse})

if result == 1:
  print "Successful"
else:
  print "Failure"
