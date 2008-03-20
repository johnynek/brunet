#!/usr/bin/python
usage = """usage:
python plab_assistant.py [--path_to_files=<filename>] [--username=<username>]
  [--port=<number>] --path_to_nodes=<filename> action
action = check, install, uninstall, gather_stats (check attempts to add the
  boot strap software to nodes that do not have it yet... a common problem on
  planetlab)
path_to_nodes = a file containing a new line delimited file containing hosts
  to install ipop (basic node) to.
username = the user name for the hosts
path_to_files = the path to a downloadable file that contains the installation
  files A sample is available at http://www.acis.ufl.edu/~ipop/planetlab/ipop/
port = port the stats app is running on
"""

import os, sys, time, signal, subprocess, re, getopt, xmlrpclib

def main():
  optlist, args = getopt.getopt(sys.argv[1:], "", ["path_to_files=", \
    "username=", "port=", "path_to_nodes="])

  o_d = {}
  for k,v in optlist:
    o_d[k] = v

  try:
    nodes_file = o_d["--path_to_nodes"]
    nodes = []
    f = open(nodes_file)
    line = f.readline()
    nodes.append(line.rstrip('\n\r '))
    for line in f:
      nodes.append(line.rstrip('\n\r '))
    f.close()

    action = args[0]
    if action == "gather_stats":
      plab = plab_assistant(action, nodes, port=(o_d["--port"]))
    else:
      username = o_d["--username"]
      path_to_files = o_d["--path_to_files"]
      plab = plab_assistant(action, nodes, username=username, \
        path_to_files=path_to_files)
  except:
    print_usage()

  plab.run()

def print_usage():
  print usage
  sys.exit()

class plab_assistant:
  def __init__(self, action, nodes, username = "", path_to_files = "", \
    port = str(0), update_callback = False):
    if action == "install":
      self.task = self.install_ipop
    elif action == "check":
      self.task = self.check_ipop
    elif action == "uninstall":
      self.task = self.uninstall_ipop
    elif action == "gather_stats":
      self.task = self.get_stats
    self.port = str(port)
    self.nodes = nodes
    self.username = username
    self.path_to_files = path_to_files
    self.update_callback = update_callback

# Runs 32 threads at the same time, this works well because half of the ndoes
# contacted typically are unresponsive and take tcp time out to fail or in
# other cases, they are bandwidth limited while downloading the data for
# install
  def run(self):
    # process each node
    pids = []
    for node in self.nodes:
      pid = os.fork()
      if pid == 0:
        self.task(node)
      pids.append(pid)
      while len(pids) >= 32:
        time.sleep(5)
        to_remove = []
        for pid in pids:
          try:
            if os.waitpid(pid, os.P_NOWAIT) == (pid, 0):
              to_remove.append(pid)
          except:
            to_remove.append(pid)
        for pid in to_remove:
          pids.remove(pid)

    # make sure we cleanly exit
    count = 0
    while True:
      if len(pids) == 0:
        break
      for pid in pids:
        to_remove = []
        try:
          if os.waitpid(pid, os.P_NOWAIT) == (pid, 0):
            to_remove.append(pid)
        except:
          to_remove.append(pid)
      for pid in to_remove:
        pids.remove(pid)
      if count == 6:
        for pid in pids:
          try:
            os.kill(pid, signal.SIGINT)
          except:
            pass
        break
      count += 1
      time.sleep(10)

  def check_ipop(self, node):
    self.ipop_install(node, True)

  def install_ipop(self, node):
    self.ipop_install(node, False)

  # node is the hostname that we'll be installing the software stack unto
  # check determines whether or not to check to see if software is already
  #   running and not install if it is.
  def ipop_install(self, node, check):
    e = ""
    username = self.username
    base_ssh = "/usr/bin/ssh -o StrictHostKeyChecking=no " + \
      "-o HostbasedAuthentication=no -o CheckHostIP=no " + username + \
      "@" + node + " "
    if check:
      try: 
        # This prints something if all is good ending this install attempt
        ssh_cmd(base_ssh + "ps uax | grep basicnode | grep -v grep")
      except:
        #print node + " already installed or fail..."
        sys.exit()
    try:
      # this helps us leave early in case the node is unaccessible
      ssh_cmd(base_ssh + "pkill -KILL basicnode &> /dev/null")
      ssh_cmd(base_ssh + "/home/" + username + "/ipop/clean.sh &> /dev/null")
      ssh_cmd(base_ssh + "rm -rf /home/" + username + "/* &> /dev/null")
      ssh_cmd(base_ssh + "wget --quiet " + self.path_to_files + " -O ~/ipop.tgz")
      ssh_cmd(base_ssh + "tar -zxf ipop.tgz")
      ssh_cmd(base_ssh + "chmod +x /home/" + username + "/ss.sh &> /dev/null")
      ssh_cmd(base_ssh + "/home/" + username + "/ss.sh &> /dev/null")
      ssh_cmd(base_ssh + "/home/" + username + "/ipop/clean.sh &> /dev/null")

  # this won't end unless we force it to!  It should never take more than 20
  # seconds for this to run... or something bad happened.
      cmd = base_ssh + " /home/" + username + "/ipop/start_ipop.sh &> /dev/null"
      pid = os.spawnvp(os.P_NOWAIT, 'ssh', cmd.split(' '))
      time.sleep(20)
      if os.waitpid(pid, os.P_NOWAIT) != (pid, 0):
        os.kill(pid, signal.SIGINT)
      print node + " done!"
      if self.update_callback:
        self.update_callback(node, 1)
    except e:
      print e
      print node + " failed!"
      if self.update_callback:
       self.update_callback(node, 0)
    sys.exit()

  def uninstall_ipop(self, node):
    username = self.username
    base_ssh = "/usr/bin/ssh -o StrictHostKeyChecking=no " + \
      "-o HostbasedAuthentication=no -o CheckHostIP=no " + username + \
      "@" + node + " "
    try:
      # this helps us leave early in case the node is unaccessible
      ssh_cmd(base_ssh + "pkill -KILL basicnode &> /dev/null")
      ssh_cmd(base_ssh + "/home/" + username + "/ipop/clean.sh &> /dev/null")
      ssh_cmd(base_ssh + "rm -rf /home/" + username + "/* &> /dev/null")
      if self.update_callback:
        self.update_callback(node, 0)
      else:
        print node + " done!"
    except:
      if self.update_callback:
        self.update_callback(node, 1)
      else:
        print node + " failed!"
    sys.exit()

  def get_stats(self, node):
    try:
      server = xmlrpclib.Server('http://' + node + ':' + self.port)
      stats = server.get_stats()
      if 'dead' in stats:
        mem = 0
        cpu = 0.0
      else:
        mem = stats['mem']
        cpu = stats['cpu']
    except:
      mem = -1
      cpu = -1.1

    data_points = {'host' : node, 'mem' : mem, 'cpu': cpu}
    if self.update_callback:
      self.update_callback(data_points)
    else:
      print data_points
    sys.exit()

# This runs the ssh command monitoring it for any possible failures and raises
# an the KeyboardInterrupt if there is one.
def ssh_cmd(cmd):
  p = subprocess.Popen(cmd.split(' '), stdout=subprocess.PIPE, stderr=subprocess.PIPE)
  os.waitpid(p.pid, 0)
  err = p.stderr.read()
  out = p.stdout.read()
  good_err = re.compile("Warning: Permanently added")
  if (good_err.search(err) == None and err != '') or out != '':
    raise KeyboardInterrupt

if __name__ == "__main__":
  main()
