#!/usr/bin/env python

# Checks a dot graph to see if it has the proper near neighbor structure

import re
import sys

files = sys.argv[1:];
edge_re = re.compile(r"""(\d+) -> (\d+) \[color= (red|blue)\];""")
node_re = re.compile(r"""(\d+) \[pos""")

for file in files:
  print "###  %s ###" % file
  myfile = open(file, 'r');

  leaf_neighbors = {}
  struct_neighbors = {}
  node_list = []
  max_node = -1
  min_node = -1
  #load the graph
  for line in myfile.readlines():
    n_m = node_re.search(line);
    if n_m:
      node = int( n_m.group(1) )
      node_list.append(node)
      if node not in leaf_neighbors:
        leaf_neighbors[node] = []
      if node not in struct_neighbors:
        struct_neighbors[node] = []
      
      if node > max_node:
        max_node = node
      if node < min_node or min_node == -1:
        min_node = node
    else:
      e_m = edge_re.search(line);
      if e_m:
        n1 = int(e_m.group(1));
        n2 = int(e_m.group(2));
        if e_m.group(3) == "red":
          if n1 in struct_neighbors:
            struct_neighbors[n1].append(n2);
          else:
            struct_neighbors[n1] = [ n2 ];
        else:
          if n1 in leaf_neighbors:
            leaf_neighbors[n1].append(n2);
          else:
            leaf_neighbors[n1] = [ n2 ];
  #Check for sanity:
  for node in node_list:
    if node in leaf_neighbors:
      print "%i has %i leafs" % (node, len(leaf_neighbors[node]))
    else:
      print "%i has 0 leafs" % (node)
    #check that A -> B means B -> A
    if node in leaf_neighbors:
      for partner in leaf_neighbors[node]:
        if node not in leaf_neighbors[partner]:
          print "leaf: %i -> %i but not vice-versa" % (node, partner)
    #check that A -> B means B -> A
    if node in struct_neighbors:
      print "%i has %i structs" % (node, len(struct_neighbors[node]))
      for partner in struct_neighbors[node]:
        if node not in struct_neighbors[partner]:
          print "struct: %i -> %i but not vice-versa" % (node, partner)
    else:
      print "%i has 0 structs" % node
    #Check that x -> x+1 and x+2:
    if node in struct_neighbors:
      target = node + 1;
      if target > max_node:
        target = target - max_node
      if target in struct_neighbors[node]:
        pass
      else:
        print "%i -> %i struct missing" % (node, target)
      target = node + 2;
      if target > max_node:
        target = target - max_node
      if target in struct_neighbors[node]:
        pass
      else:
        print "%i -> %i struct missing" % (node, target)
  myfile.close()
