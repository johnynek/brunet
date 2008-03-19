#!/usr/bin/python
#Modified from exmaple code at http://docs.python.org/lib/simple-xmlrpc-servers.html
import time
from SimpleXMLRPCServer import SimpleXMLRPCServer

# Create server
server = SimpleXMLRPCServer(('localhost', 8000))
server.register_introspection_functions()

# Register pow() function; this will use the value of 
# pow.__name__ as the name, which is just 'pow'.
server.register_function(pow)

# Register a function under a different name
def adder_function(x,y):
    return x + y
server.register_function(adder_function, 'add')

# Register an instance; all the methods of the instance are 
# published as XML-RPC methods (in this case, just 'div').
class MyFuncs:
    def div(self, x, y): 
        return x // y
    
server.register_instance(MyFuncs())

def wait_and_return(waiting_time_in_secs):
	print waiting_time_in_secs.__class__
	print waiting_time_in_secs
	time.sleep(waiting_time_in_secs)
	return 'slept for ' + str(waiting_time_in_secs) + ' seconds'
server.register_function(wait_and_return, 'wait_and_return')

def no_arg_function():
	return 'Hello World!'
server.register_function(no_arg_function, 'no_arg')

# Run the server's main loop
server.serve_forever()
