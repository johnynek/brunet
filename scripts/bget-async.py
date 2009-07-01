#!/usr/bin/env python
import xmlrpclib, getopt, sys
rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")
#pydht = xmlrpclib.Server('http://128.227.56.152:64221/xd.rem')

#usage:
# bget.py bget.py [--output=<filename to write value to>] [--quiet (only print the value)] <key>
# you must either have a value string, or an input.

optlist, args = getopt.getopt(sys.argv[1:], "", ["output=", "quiet"])
o_d = {}
for k,v in optlist:
  o_d[k] = v

if (len(args) < 1):
  print """usage:\n
  \tbget.py [--output=<filename to write value to>] [--quiet (only print the value)] <key>"""
  sys.exit(1)

res = rpc.localproxy("DhtClient.BeginGet", xmlrpclib.Binary(args[0]))
value_dict = rpc.localproxy("DhtClient.ContinueGet", res)
while len(value_dict) > 0:
  value = value_dict['value'].data
  ttl = value_dict['ttl']
  print value
  value_dict = rpc.localproxy("DhtClient.ContinueGet", res)
rpc.localproxy("DhtClient.EndGet", res)
