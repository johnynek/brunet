#!/usr/bin/python
import xmlrpclib

# 10000 is the default port for XmlRpcManager
url = 'http://127.0.0.1:10000/xm.rem'
rpc = xmlrpclib.Server(url)
# You have a server named 'host' at http://localhost:8000 that has the get_stats method
try:
	rpc.localproxy("xmlrpc.AddXRHandler", 'host','http://127.0.0.1:8000')
	print "Reigstered service at http://127.0.0.1:8000 with the name host. Listening..."
except xmlrpclib.Fault:
	#do nothing
	pass
print "Calling function no_arg on host"
print rpc.localproxy('host.no_arg')
print "Calling function add on host"
print rpc.localproxy('host.add', 1, 2)
print "Calling function wait_and_return on host"
print rpc.localproxy('host.wait_and_return', 5)
print "Removing handler"
rpc.localproxy("xmlrpc.RemoveXRHandler", 'host','http://127.0.0.1:8000')
print "Calling again..."
try:
	print rpc.localproxy('host.add', 1, 2)
except xmlrpclib.Fault, ex:
	print "Exception message:"
	print ex
print "You see the above exception as expected."
