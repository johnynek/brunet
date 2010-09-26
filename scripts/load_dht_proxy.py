#!/usr/bin/python
import xmlrpclib, pickle, sys, socket

rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")
socket.setdefaulttimeout(15) 
f = open(sys.argv[1], 'r')
data = pickle.load(f)
f.close()
for entry in data:
  rpc.localproxy("RpcDhtProxy.Register", entry["Key"], entry["Value"], entry["Ttl"])
