#!/usr/bin/env python

import xmlrpclib, SimpleXMLRPCServer, threading

#############
# Handle the callbacks here:

callback_handler = SimpleXMLRPCServer.SimpleXMLRPCServer(("localhost", 20001))
def print_event(event_type, args, state):
  print "type: %s\nargs: %s\n state: %s\n\n" % (event_type, args, state)
  return True;

callback_handler.register_function(print_event);

server_thread = threading.Thread(target=callback_handler.serve_forever)
server_thread.start()

############

node = xmlrpclib.ServerProxy("http://127.0.0.1:20000/xm.rem") 
#setup our callback function:
node.localproxy("xmlrpc.AddXRHandler", "pytest", "http://localhost:20001/RPC2")
print node.localproxy("ConnectionTable.addConnectionHandler", \
                "pytest.print_event", "my_state")

raw_input("Press Enter to quit")

#stop listening now
server_thread.join(1.0)
