#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a netmodeler 
# directed graph. The current version takes the whole log.
#
import sys
from sets import Set
infilename  = sys.argv[1]

ifile = open( infilename, 'r')  # r for reading
nmstructfile = open ( infilename+"_struct.nm",'w')
nmunstructfile = open ( infilename+"_unstruct.nm",'w')
nmleaffile = open ( infilename+"_leaf.nm",'w')
constr = ':'


pendingwrite = False

leafcon = Set()
leafdiscon = Set()
structcon = Set()
structdiscon = Set()
unstructcon = Set()
unstructdiscon = Set()

#temp set to get the current existing edges
tmpset = Set()

tempstart = 0
# read the data

for line in ifile:
  parsed_line = line.split()
  if parsed_line[0] == 'local_address' :
    if pendingwrite == True :
      tmpset = leafcon-leafdiscon
      for connection in tmpset :
        nmleaffile.write( '%i %s %i\n' % (tempstart,constr, connection ) )
      tmpset = structcon-structdiscon
      for connection in tmpset :
        nmstructfile.write( '%i %s %i\n' % (tempstart,constr, connection ) )
      tmpset = unstructcon-unstructdiscon
      for connection in tmpset :
        nmunstructfile.write('%i %s %i\n' % (tempstart,constr, connection ) )
    leafcon = Set()
    leafdiscon = Set()
    structcon = Set()
    structdiscon = Set()
    unstructcon = Set()
    unstructdiscon = Set()
    tempstart = int(parsed_line[1])
  else : 
    if parsed_line[3] == 'Leaf' :
      pendingwrite = True    
      if parsed_line[2] == 'connection' :
        leafcon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'disconnection' :
        leafdiscon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'receiving_connection' :
        pass
      elif parsed_line[2] == 'initiating_connection' :
        pass
      else:
        pendingwrite = False    
        print 'Error: unknown edge dynamic'
    elif parsed_line[3] == 'Structured' :
      pendingwrite = True    
      if parsed_line[2] == 'connection':
        structcon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'disconnection':
        structdiscon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'receiving_connection' :
        pass
      elif parsed_line[2] == 'initiating_connection' :
        pass
      else:
        pendingwrite = False    
        print 'Error: unknown edge dynamic'
    elif parsed_line[3] == 'Unstructured' :
      pendingwrite = True    
      if parsed_line[2] == 'connection':
        unstructcon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'disconnection':
        unstructdiscon.add(int(parsed_line[4]) )
      elif parsed_line[2] == 'receiving_connection' :
        pass
      elif parsed_line[2] == 'initiating_connection' :
        pass
      else:
        pendingwrite = False    
        print 'Error: unknown edge dynamic'
    else:
      print 'Error: unknown connection type'


#write the last node log  
tmpset = leafcon-leafdiscon
for connection in tmpset :
  nmleaffile.write( '%i %s %i\n' % (tempstart,constr, connection ) )
tmpset = structcon-structdiscon
for connection in tmpset :
  nmstructfile.write( '%i %s %i\n' % (tempstart,constr, connection ) )
tmpset = unstructcon-unstructdiscon
for connection in tmpset :
  nmunstructfile.write('%i %s %i\n' % (tempstart,constr, connection ) )

nmleaffile.close()
nmstructfile.close()
nmunstructfile.close()
