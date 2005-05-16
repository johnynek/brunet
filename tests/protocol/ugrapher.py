#!/usr/bin/env python

#Checks the unstructured part of a network.
from __future__ import division
import re
import sys

files = sys.argv[1:];
edge_re = re.compile(r"""(\d+) -> (\d+) \[color= (red|blue|green)\];""")
node_re = re.compile(r"""(\d+) \[pos""")
max_k = 0
for file in files:
  print "###  %s ###" % file
  myfile = open(file, 'r');

  neighbors = {}
  node_list = []
  #load the graph
  for line in myfile.readlines():
    n_m = node_re.search(line);
    if n_m:
      node = int( n_m.group(1) )
      node_list.append(node)
      if node not in neighbors:
        neighbors[node] = []
    else:
      e_m = edge_re.search(line);
      if e_m:
        n1 = int(e_m.group(1));
        n2 = int(e_m.group(2));
        if e_m.group(3) == "green":
          if n1 in neighbors:
            neighbors[n1].append(n2);
          else:
            neighbors[n1] = [ n2 ];
  #Check for sanity:
  for node in node_list:
    if node in neighbors:
      if len(neighbors[node]) > max_k:
        max_k = len(neighbors[node])
      print "#%i has %i cons" % (node, len(neighbors[node]))
    else:
      print "#%i has 0 cons" % (node)
    #check that A -> B means B -> A
    if node in neighbors:
      for partner in neighbors[node]:
        if node not in neighbors[partner]:
          print "#%i -> %i but not vice-versa" % (node, partner)
  #Print the CDF 
  deg_count = [0 for i in xrange(max_k + 1)];
  n_tot = len(node_list)
  for (node, neighs) in neighbors.iteritems():
    k = len(neighs);
    deg_count[k] = deg_count[k] + 1;
  rem = n_tot
  ccdf = open(file + ".ccdf", 'w') 
  for i in range(max_k + 1):
    rem = rem - deg_count[i]
    ccdf.write( "%i %f\n" % (i, rem/n_tot) )
  #Here is the gnuplot part:
  gnuplot = open(file + ".gnuplot", 'w')
  gnuplot.write("# run this by typing: load \"%s.gnuplot\" in gnuplot\n" % (file));
  gnuplot.write("f(x) = a*x**b\n");
  gnuplot.write("a=1\n");
  gnuplot.write("b=-2\n");
  gnuplot.write("set logscale xy\n");
  gnuplot.write("set grid\n");
  gnuplot.write("set title \"CCDF of node degree\"\n");
  gnuplot.write("set xlabel \"degree (k)\"\n"); 
  gnuplot.write("set ylabel \"Fraction of nodes with degree greater than k\"\n"); 
  gnuplot.write("fit [1:] f(x) \"%s.ccdf\" via a,b\n" % (file));
  gnuplot.write("plot f(x), \"%s.ccdf\" w l\n" % (file))
  gnuplot.close();
  print "# run this by typing: load \"%s.gnuplot\" in gnuplot\n" % (file)
  myfile.close()
