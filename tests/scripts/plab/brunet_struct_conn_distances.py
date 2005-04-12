#!/usr/bin/env python
# 
# This takes a results.txt.tmp_big temporary file and parses it to find the distances
# between edges.
#
import sys, math, re, stats
from sets import Set
   
class BruNetEdge:
  def __init__(self,start,end):
    self.start = start
    self.end = end
  def distance(self):
    small = 0
    big = 0
    biggest = 2**160 - 2
    if self.start > self.end :
      small = self.end
      big = self.start
    elif self.start < self.end:
      small = self.start
      big = self.end
    else:
      print "error"
    #print "biggest ",biggest," big ",big," small ",small   
    length_through_zero = small + biggest-big + 1
    #print "through zero", length_through_zero
    length_around_ring = big - small
    #print "around zero", length_around_ring
    if length_through_zero > length_around_ring :
      return length_around_ring
    else:
      return length_through_zero

shortcuts = Set()
infilename  = sys.argv[1]

icfile = open( infilename, 'r')  # r for reading

all_nodes = {}

distlist = []
edgelist = []

linenum = 0
local_address = 0
pendingwrite = False
for line in icfile:
  linenum = linenum + 1
  #print linenum
  parsed_line = line.split()
  if parsed_line[0] == 'local_address' :
    if pendingwrite == True :
      if len(shortcuts) > 0:
        for address in shortcuts:
          tmpedge = BruNetEdge( local_address,address)
          distlist.append( tmpedge.distance() )
      shortcuts.clear()    
      local_address = int(parsed_line[1])
    else:
      pendingwrite = True
  else:
     if len(parsed_line) > 5:
       if parsed_line[5] == 'structured.shortcut' :
         t_add = int(parsed_line[4])
         if parsed_line[2] == 'connection' :
           if t_add not in shortcuts:
             shortcuts.add(t_add)
         elif parsed_line[2] == 'disconnection' :
           shortcuts.discard(t_add)
         else:
           pass
if len(shortcuts) > 0:
  for address in shortcuts:
    tmpedge = BruNetEdge( local_address,address)
    distlist.append( tmpedge.distance() )

distlist.sort()

for n in distlist:
  print n

#cumhist,l,b,e = stats.cumfreq( distlist,10)
#for histval in cumhist:
#  print histval
icfile.close()
