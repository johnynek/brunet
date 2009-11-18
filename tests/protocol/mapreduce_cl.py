#!/usr/bin/env python

import xmlrpclib, sys, SimpleXMLRPCServer, random

def make_proxy(port):
    return xmlrpclib.ServerProxy("http://127.0.0.1:20000/xm%i.rem" % (port))

node = make_proxy(int(sys.argv[1]))
#do traceroute:
call_info = {'task_name' : 'pymrtest',
             'gen_arg' : sys.argv[2] }
print node.localproxy('mapreduce.Start', call_info)

