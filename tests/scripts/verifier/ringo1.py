#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a graph
# The current version takes the whole log.
#
import sys, math, re
from sets import Set
infilename  = sys.argv[1]


icfile = open( infilename, 'r')  # r for reading
outfilename = infilename + 'circle'
ofile = open( outfilename, 'w') # w for writing
firstline = 'graph test {\n'
ofile.write(firstline)

nodelist = []

for line in icfile:
  parsed_line = line.split()
  if parsed_line[0] == 'digraph' :
    pass
  elif parsed_line[0] == '}':
    pass
  else : 
    if parsed_line[0] in nodelist:
      pass
    else:  
      nodelist.append( parsed_line[0] )
icfile.close()


nodes = len(nodelist)

nodesize =  8.0/(float(nodes)) 
canvassize = 20*nodes
fontsize = 20*nodesize
r = canvassize/2.0 - 1.0 -36.0*nodesize
c = int(canvassize/2)
position = 0
phi = math.pi/(2*float(nodes))
theta = 0.0
ringlayoutx = 0
ringlayouty = 0
indexmap = {}
# (int(eachnode),ringlayoutx,ringlayouty,nodesize,nodesize,fontsize)
#    ofile.write( p.sub('--',line) )

for eachnode in nodelist:
  indexmap[int(eachnode)] = position
  theta = float( 4*position*phi)
  ringlayoutx = c + int(r*math.sin(theta) );
  ringlayouty = c - int(r*math.cos(theta) );
  node_line = "%i [pos=\"%i,%i \", \
    width=\"%1.2f\",height=\"%1.2f\",fontsize=\"%i\"];\n" % \
    (position,ringlayoutx,ringlayouty,nodesize,nodesize,fontsize)
  ofile.write(node_line)
  position += 1;

constr = '--'

ifile = open( infilename, 'r')  # r for reading

# read the data
p = re.compile('->')
for line in ifile:
  parsed_line = line.split()
  if parsed_line[0] == 'digraph' :
    pass
  elif parsed_line[0] == '}':
    pass
  else : 
    ofile.write( str(indexmap[int(parsed_line[0])]) )
    ofile.write( "--" )
    ofile.write( str(indexmap[int(parsed_line[2])]) )
    ofile.write( '\n' )
    
    
ifile.close()
ofile.write('}\n')
ofile.close()
