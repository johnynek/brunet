#!/usr/bin/python
import sys, os, crawl, csv, datetime, time, re, subprocess, plab_assistant, signal

usage = """usage:
plab_deployer slice_name base_path unique_name path_to_files
slice_name = The username that you use on planetlab nodes.
base_path = A base directory that files used in this deployment reside. Please provide absolute path.
unique_name = A unique name that will be used as Brunet namespace, etc.
path_to_files = A url that the files will be uploaded to
ssh_key = A path to a local ssh-kkey
"""

class plab_deployer:
  def __init__(self, slice_name, base_path, name, path_to_files, ssh_key):
    self.slice_name = slice_name
    self.base_path = base_path
    self.name = name
    self.path_to_files = path_to_files
    self.ssh_key = ssh_key
    
  def build(self):
    """  Compiles the files in the zip file using mkbundle, and compress to tgz file. """
    os.chdir(self.base_path)
    os.system("unzip -o -d binary input.zip")
    os.chdir("binary")
    dlls = ""
    for i in os.walk('.'):
      files = i[2]
      for file in files:
        if file.endswith(".dll"):
          dlls += file + " "

    os.system("mkbundle2 -o basicnode --deps --config-dir . --static -z BasicNode.exe " + dlls)
    os.system("cp basicnode ../node/.")
    os.chdir("..")
    os.system("sed 's/BRUNETNAMESPACE/" + self.name + "/' -i node/node.config." + self.slice_name)
    os.system("tar -czf output.tgz node")

  def run(self):
    """  deploys bundled basicnode module to plab nodes using plab_assistant.
         crawls the ring continuously for 48 hours and then gathers logs. """
    plab = plab_assistant.plab_assistant("install", nodes=None, username=self.slice_name, \
        path_to_files=self.path_to_files, ssh_key=self.ssh_key)
    plab.run()
    os.chdir(self.base_path + "/node")
    os.system("sed 's/<Enabled>false/<Enabled>true/' -i node.config." + self.slice_name)
    # start local basicnode without cronolog...
    start_basicnode = "./basicnode node.config." + self.slice_name + " &> log &"
    p = subprocess.Popen(start_basicnode.split(' '))
    self.local_basicnode_pid = p.pid
    time.sleep(60)

    os.chdir(self.base_path)
    start_utc = datetime.datetime.utcnow()
    test_length = datetime.timedelta(hours = 48)
    content = open("node/node.config." + self.slice_name).read()
    port_line = re.search("<XmlRpcManager>.*</XmlRpcManager>", content, re.S).group()
    port =  int(re.search("\d+", port_line).group())
    while datetime.datetime.utcnow() - start_utc < test_length:
      nodes = crawl.crawl(port)
      consistency, count = crawl.check_results(nodes)
      os.chdir(self.base_path)
      f = open("crawl.csv", "a")
      f.write(str(time.asctime()) + ", " + str(consistency) + ", " + str(count) + "\n")
      f.close()
      time.sleep(60 * 15)
    # done with the test, start getting logs and cleaning up.
    plab = plab_assistant.plab_assistant("get_logs", nodes=None, username=self.slice_name, \
        path_to_files=self.path_to_files, ssh_key=self.ssh_key)
    plab.run()
    os.system("zip -r9 results.zip logs output.log crawl.csv")
    # Actually not necessary because installation cleans nodes first.
    plab = plab_assistant.plab_assistant("uninstall", nodes=None, username=self.slice_name, \
        path_to_files=self.path_to_files, ssh_key=self.ssh_key)
    plab.run()
    try:
      os.kill(self.local_basicnode_pid, signal.SIGKILL)
    except:
      pass
    
if __name__ == '__main__':
  args = sys.argv[1:]
  try:
    plab = plab_deployer(args[0], args[1], args[2], args[3], args[4])
  except:
    print usage
    sys.exit()
  plab.build()
  print "Upload output.tgz to " + plab.path_to_files + " a web server and provide the link."
  raw_input("Press any key to continue")
  plab.run()
