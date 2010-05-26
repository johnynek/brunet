#!/usr/bin/env python
"""
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

Large portions of this program are borrowed from:
__author__ = "Chad J. Schroeder"
__copyright__ = "Copyright (C) 2005 Chad J. Schroeder"

__revision__ = "$Id$"
__version__ = "0.2"

Disk And Execution MONitor (Daemon)

Configurable daemon behaviors:

   1.) The current working directory set to the "/" directory.
   2.) The current file creation mode mask set to 0.
   3.) Close all open files (1024). 
   4.) Redirect standard I/O streams to "/dev/null".
   5.) Set gid and uid

A failed call to fork() now raises an exception.

References:
   1) Advanced Programming in the Unix Environment: W. Richard Stevens
   2) Unix Programming Frequently Asked Questions:
         http://www.erlenstar.demon.co.uk/unix/faq_toc.html
"""

# Standard Python modules.
import os               # Miscellaneous OS interfaces.
import sys              # System-specific parameters and functions.
import resource         # Resource usage information.
import getopt
import grp
import pwd

# Default daemon parameters.
# File mode creation mask of the daemon.
UMASK = 0

# Default maximum for the number of available file descriptors.
MAXFD = 1024

# The standard I/O file descriptors are redirected to /dev/null by default.
if (hasattr(os, "devnull")):
  REDIRECT_TO = os.devnull
else:
  REDIRECT_TO = "/dev/null"

if __name__ == "__main__":
  """Detach a process from the controlling terminal and run it in the
  background as a daemon.
  """

  opt, args = getopt.getopt(sys.argv[1:], "", ["user=", "group="])
  opt_dict = {}
  filter(opt_dict.update, map(lambda md: {md[0] : md[1]}, opt))
  if "--user" in opt_dict:
    uid = pwd.getpwnam(opt_dict["--user"]).pw_uid
    gid = pwd.getpwnam(opt_dict["--user"]).pw_gid
    if "--group" in opt_dict:
      gid = grp.getgrnam(opt_dict["--group"]).gr_gid
    # Below: order matters
    os.setgid(gid)
    os.setuid(uid)
  for arg in args:
    cmd = arg + " "

  try:
    if os.fork() > 0:
      os._exit(0)
  except OSError, e:
     raise Exception, "%s [%d]" % (e.strerror, e.errno)

  os.setsid()
  try:
    if os.fork() > 0:
      os._exit(0)
  except OSError, e:
    raise Exception, "%s [%d]" % (e.strerror, e.errno)

  os.umask(UMASK)

  maxfd = resource.getrlimit(resource.RLIMIT_NOFILE)[1]
  if maxfd == resource.RLIM_INFINITY:
    maxfd = MAXFD
  
  # Iterate through and close all file descriptors.
  for fd in range(0, maxfd):
    try:
      os.close(fd)
    except OSError: # ERROR, fd wasn't open to begin with (ignored)
      pass

  sys.stdout.flush()
  sys.stderr.flush()

  si = file(REDIRECT_TO, 'r')
  so = file(REDIRECT_TO, 'a+')
  se = file(REDIRECT_TO, 'a+', 0)

  os.dup2(si.fileno(), sys.stdin.fileno())
  os.dup2(so.fileno(), sys.stdout.fileno())
  os.dup2(se.fileno(), sys.stderr.fileno())

  os.system("nohup " + cmd + " &")
  sys.exit(0)
