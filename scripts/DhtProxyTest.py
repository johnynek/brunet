#!/usr/bin/python
import xmlrpclib, time, random, socket
from datetime import datetime

keys=500
values=3
ttl_min=120
ttl_max=600

socket.setdefaulttimeout(10)
rpc = xmlrpclib.Server("http://127.0.0.1:10000/xm.rem")

def main():
  start = datetime.now()

  for i in range(keys):
    print "Inserting key:  " + str(i)
    for j in range(values):
      put(str(i), str(j), random.randint(ttl_min, ttl_max))

  while True:
    count = 0
    for i in range(keys):
#      print "Retrieving key:  " + str(i)
      res = get(str(i))
      ex_res = range(values)
      for index in range(len(res)):
        ex_res[int(res[index]['value'].data)] = 0
      lcount = 0
      for index in ex_res:
        if ex_res[index] != 0:
          lcount += 1
#      print "Results for " + str(i) + " = " + str(lcount)
      count += lcount
    print "Results for pool at " + str(datetime.now() - start) + " time since start = " + str(count)
    print "Sleeping for 5 minutes"
    time.sleep(60)

def get(key):
  return rpc.localproxy("DhtClient.Get", xmlrpclib.Binary(key))

def put(key, value, ttl):
  rpc.localproxy("RpcDhtProxy.Register", xmlrpclib.Binary(key), xmlrpclib.Binary(value), ttl)

def remove(key, value):
  print rpc.localproxy("RpcDhtProxy.Unregister", xmlrpclib.Binary(key), xmlrpclib.Binary(value))

if __name__ == "__main__":
  main()
