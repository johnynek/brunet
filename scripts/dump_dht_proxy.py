#!/usr/bin/python
import xmlrpclib, pickle, sys, socket 

rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")
socket.setdefaulttimeout(60) 
res = rpc.localproxy("RpcDhtProxy.ListEntries")
f = open(sys.argv[1], 'w+')
pickle.dump(res, f)
f.close()
