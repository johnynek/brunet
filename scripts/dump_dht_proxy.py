#!/usr/bin/python
import xmlrpclib, pickle, sys

rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")
res = rpc.localproxy("RpcDhtProxy.ListEntries")
f = open(sys.argv[1], 'w+')
pickle.dump(res, f)
f.close()
