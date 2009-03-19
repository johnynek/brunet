#!/usr/bin/env python
import xmlrpclib, getopt, sys
pydht = xmlrpclib.Server('http://localhost:64221/xd.rem')
#pydht = xmlrpclib.Server('http://128.227.56.152:64221/xd.rem')

#usage:
# bput.py [--ttl=<time in sec>] [--input=<filename, - for stdin>] <key> [<value>]
# you must either have a value string, or an input.

optlist, args = getopt.getopt(sys.argv[1:], "", ["ttl=", "input="])
o_d = {}
for k,v in optlist:
  o_d[k] = v

if (len(args) < 1) and ("--input" not in o_d):
  print """usage:\n
  \tbput.py [--ttl=<time in sec>] [--input=<filename, - for stdin>] <key> [<value>]
  \tyou must either have a value string, or an input."""
  sys.exit(1)

if "--input" in o_d:
  if o_d["--input"] == "-":
    #read stdin:
    f = sys.stdin
  else:
    f = open( o_d["--input"], 'r')
  #dump the whole file into a string
  import StringIO
  file = StringIO.StringIO()
  for line in f:
    file.write(line)
  value = file.getvalue()
else:
  value = args[1]

if "--ttl" in o_d:
  ttl = int(o_d["--ttl"])
else:
  #default TTL= 1 day
  ttl = 86400 
# put (mykey,myvalue) pair into the DHT, with time-to-live of 100000 seconds
print pydht.Put(xmlrpclib.Binary(args[0]), xmlrpclib.Binary(value), ttl)
