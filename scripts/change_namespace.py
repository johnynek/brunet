#!/usr/bin/env python

import sys, tempfile, os, re, os.path
from functools import partial

filestorename = sys.argv[3:]

def get_namespace(sourcef):
  ns_str = "namespace ([^{;]+)\\b"
  for line in sourcef:
    m = re.search(ns_str, line)
    if m:
      return m.group(1)

def fixnamespace(oldns, newns, sourcef):
  (tempf, tempn) = tempfile.mkstemp()
  oldstr = r"(.*)namespace %s\b(.*)" % oldns
  newstr = r"\1namespace %s\2" % newns
  for line in sourcef:
    newline = re.sub(oldstr, newstr, line)
    os.write(tempf, newline)
  os.close(tempf)
  return tempn

def is_using(sourcef, ns):
  using_ns = "using %s;" % ns
  return any(using_ns in line for line in sourcef)

def add_using(fn, ns):
  (tempf, tempn) = tempfile.mkstemp()
  sourcef = open(fn)
  not_done = True
  for line in sourcef:
    if not_done and "namespace" in line:
      #write using just before:
      os.write(tempf, "using %s;\n" % ns)
      not_done = False
    os.write(tempf,line)
  os.close(tempf)
  #print "rename %s -> %s" % (tempn, fn)
  os.rename(tempn, fn)

def change_ns(fn, old, new):
  temp = fixnamespace(old, new, open(fn))
  os.rename(temp, fn)
  
def fixns_main(argv):
  for fn in argv[3:]:
    change_ns(fn, argv[1], argv[2])
def printns_main(argv):
  for fn in argv[1:]:
    print get_namespace(open(fn))
def add_using_main(argv):
  namespace = argv[1]
  for fn in argv[2:]:
    if not is_using(open(fn), namespace):
      add_using(fn, namespace)

def rec_split(dirstr, list):
  if dirstr == '':
    return list
  else:
    (path, this_part) = os.path.split(dirstr)
    list.insert(0,this_part)
    return rec_split(path, list)

def print_error(fn, actual_ns, expected_ns):
  print "%s ns=%s should be %s" % (fn, actual_ns, expected_ns)

 
def check_tree_main(on_err, args):
  base = args[1]
  ftype = ".cs"
  for (dpath, dnames, fnames) in os.walk(os.curdir):
    nslist = rec_split(dpath, [])
    nslist[0] = base
    ns = ".".join(nslist)
    for fn in fnames:
      if not fn.endswith(ftype):
        continue
      fullnm = os.path.join(dpath, fn)
      this_ns = get_namespace(open(fullnm))
      if this_ns != ns:
        on_err(fullnm, this_ns, ns)

if __name__ == "__main__":
  modes = { 'change_ns_f' : fixns_main,
            'print_ns_f' : printns_main,
            'check_tree' : partial(check_tree_main, print_error),
            'change_tree' : partial(check_tree_main, change_ns),
            'add_using' : add_using_main,
          }
  mode = sys.argv.pop(1)
  modes[mode](sys.argv)
