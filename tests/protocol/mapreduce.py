#!/usr/bin/env python

import xmlrpclib, sys

node = xmlrpclib.ServerProxy("http://127.0.0.1:20000/xm.rem")

#do an example call:
dest = sys.argv[1]
#args = { 'task_name' : "Brunet.MapReduceTrace",
args = { 'task_name' : "Brunet.MapReduceRangeCounter",
         'gen_arg' : dest }
print node.uriproxy("sender:localnode", 1, "mapreduce.Start", args)

