#!/usr/bin/env python

import xmlrpclib, sys, SimpleXMLRPCServer, random

node_cnt = int(sys.argv[1])
handler_name = "pymapred1"

def make_proxy(port):
    return xmlrpclib.ServerProxy("http://127.0.0.1:20000/xm%i.rem" % (port))

#create the list of all the node proxies:
port_list = range(node_cnt)
proxy_list = [ make_proxy(i) for i in port_list ]
addr_list = [ prox.localproxy("sys:link.GetNeighbors")['self'] for prox in proxy_list ]
print addr_list
#Here is how Brunet calls into our code:
srv_port = 30000
server = SimpleXMLRPCServer.SimpleXMLRPCServer(('localhost', srv_port))
server_url = 'http://localhost:%i/RPC2' % srv_port
#Tell each node about our server:
for (proxy, addr) in zip(proxy_list, addr_list):
    print "adding: %s to: %s" % (handler_name, addr)
    proxy.localproxy("xmlrpc.AddXRHandler", handler_name, server_url)
#Let's set up our map functions:
def add_addr_mapf(in_list):
    fns = []
    for addr in in_list:
        #define the function:
        #return a list with one item, reduce will concat them
        def _tmp_map(map_args, res=addr):
            return [res]
        #give it a random name:
        name = 'map%i' % random.randint(0,2**32)
        server.register_function(_tmp_map, name)
        fns.append(name)
    return fns

map_names = add_addr_mapf(addr_list)
#set up the map-tree-reduce computation on each node:
def set_up_mtr(proxy_list, map_names):
    for (prx, mapfn) in zip(proxy_list, map_names):
        hnd = {'task_name' : 'pymrtest',
               'map' : ['sender:localnode', '%s.%s' % (handler_name, mapfn) ],
               'tree' : ['sender:localnode', 'mapreduce.tree:Brunet.MapReduceGreedy' ],
               'reduce' : ['sender:localnode', 'mapreduce.reduce:Brunet.MapReduceListConcat' ]
               }
        print "adding: %s" % hnd
        prx.localproxy('mapreduce.AddHandler', hnd);

set_up_mtr(proxy_list, map_names)
print "Set up the mapreduce task on %i nodes" % len(proxy_list)
#Looks like we're all set:
server.serve_forever()
