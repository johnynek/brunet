#!/usr/bin/env python
#
# This script attempts to find a large complete subgraph
# of planetlab nodes for experiments.
#
# As input this script takes a directory which contains a file for each node
# that tried to communicate with other nodes in a previous screenconnectivity
# test. Each of these files has all of the hostnames of the nodes that could
# be reached by the node represented by that file.
#
# It also takes in the entire set of nodes that were initially tried in the
# screening procedure.  This set must be a superset of each of the sets in the
# files mentioned above.
#
# Nodes that failed to communicate with a minimum number of nodes
# (min_set_size) will not be considered in the intersection
#
# The output is a list of nodes that form a complete graph in planetlab.  That
# is each pair of nodes from the list can communicate with each other.


# script does an heuristic search to find the best threshold (min_set_size)
# the sets are order from largest to smallest and the intersection of the sets
# is successively performed untill the number of nodes in the intersection set
# is less than or equal to the number of sets that were intersected.
import sys, os
from sets import Set

#directory where all of the ping logs are found
dirname  = sys.argv[1]

tmp_file_names = os.listdir(dirname)

set_dict = {}

tmp_file_names = os.listdir(dirname)
for tmp_file_name in tmp_file_names:
  tmp_file = open(dirname +'/'+tmp_file_name,'r')
  t_list = tmp_file.readlines()
  t_set = Set(t_list)
  tmp_set_list = []
  if len(t_set) in set_dict:
    tmp_set_list = set_dict[len(t_set)]
  tmp_set_list.append(tmp_file_name)
  set_dict[len(t_set)] = tmp_set_list
    
set_dict_keys = set_dict.keys()
set_dict_keys.sort()
set_dict_keys.reverse()
count = 1
global_set = Set()
for sd_key in set_dict_keys:
  #print sd_key, set_dict[sd_key]
  tmp_sorted_file_names = set_dict[sd_key]
  for tmp_sorted_file_name in tmp_file_names:
    tmp_file = open(dirname +'/'+tmp_sorted_file_name,'r')
    t_list = tmp_file.readlines()
    t_set = Set(t_list)
    if count == 1:
      global_set = t_set
    else:
      global_set.intersection_update(t_set)
    #print count, len(global_set)
    count = count + 1
    if count >= len(global_set):
      for plnode in global_set:
        print plnode.strip()
      sys.exit(0)  
