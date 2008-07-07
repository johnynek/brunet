#!/usr/bin/python
import SimpleXMLRPCServer, BaseHTTPServer, SimpleHTTPServer, os, sys, re, subprocess

def main():
  whoami = sys.argv[1]
  if whoami == "ufl_testwow":
    port = 44385
  elif whoami == "ufl_fusedht":
    port = 44387
  elif whoami == "ufl_wow":
    port = 44389
  else:
    sys.exit()
  pid = os.fork()
  if pid == 0:
    XMLRPCServer(port)
  else:
    pid = os.fork()
    if pid == 0:
      HTTPServer(port + 1)

def XMLRPCServer(port):
  space_re = re.compile('\s+')
  data_re = re.compile('\S+')
  # Create server
  server = SimpleXMLRPCServer.SimpleXMLRPCServer(('', port))

  class simplenode:
    def get_stats(self):
      proc = subprocess.Popen("ps uax | grep basicnode | grep -v grep", \
        bufsize=32678, stdout=subprocess.PIPE, stderr=subprocess.PIPE, \
        shell=True)
      os.waitpid(proc.pid, 0)
      res = proc.stdout.read()
      proc.stdout.close()
      proc.stderr.close()
      count = 0
      rv = {}
      if len(res) == 0:
        rv['dead'] = True
      while len(res) > 0:
        try:
          data = data_re.search(res).group()
          res = res[space_re.search(res).end():]
          if count == 2:
            rv['cpu'] = data
          elif count == 5:
            rv['mem'] = data
          elif count == 8:
            rv['start'] = data
            break
          count += 1
        except:
          rv['error'] = True
          rv['count'] = count
          break
      return rv

  server.register_instance(simplenode())

  # Run the server's main loop
  server.serve_forever()

def HTTPServer(port):
  HandlerClass = SimpleHTTPServer.SimpleHTTPRequestHandler
  ServerClass = BaseHTTPServer.HTTPServer
  protocol="HTTP/1.0"
  HandlerClass.protocol_version = protocol
  server = ServerClass(('', port), HandlerClass)
  server.serve_forever()

if __name__ == '__main__':
    main()
