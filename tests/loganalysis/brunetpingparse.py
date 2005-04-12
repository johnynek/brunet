#!/usr/bin/env python
# 
# This takes a Brunet connection log and parses it into a graph
# The current version takes the whole log.
#
import sys, time, copy, stats
from datetime import timedelta, datetime

    
class BruNetPingParse:
    def __init__(self,infilename):
        self.ifile = open( infilename, 'r')
        self.sent_uid_to_time = {}
        self.received_uid_in_sent_to_time = {}
        self.time_to_sent_uid = {}
        self.ping_times = []
        self.local_node = None
        self.remote_node = None
    def parse(self):
        line = self.ifile.readline()
        while line:
            self.process_line(line)
            line = self.ifile.readline()
        self.output_results()
        
    def process_line(self,line):
        parsed_line = line.split()
        if parsed_line[0] == 'local:' :
            self.process_header_line(parsed_line)
        else:
            self.process_data_line(parsed_line)
    
    def output_results(self):
        c1 = len(self.sent_uid_to_time) > 0
        c2 = len(self.received_uid_in_sent_to_time) > 0
        if c1 and c2:
            sorted_ids = self.time_to_sent_uid.keys()
            sorted_ids.sort()
            for s_time in sorted_ids:
                rec = self.time_to_sent_uid[s_time]
                t_time = -1
                if rec in self.received_uid_in_sent_to_time:
                    t_time = self.received_uid_in_sent_to_time[rec]- \
                      self.sent_uid_to_time[rec]    
                #    if t_time > 0.0:
                self.ping_times.append(t_time)
            self.sent_uid_to_time = {}
            self.received_uid_in_sent_to_time = {}
            self.time_to_sent_uid = {}    
            if len(self.ping_times) > 0:
                tmp_str = ""
                for f_time in self.ping_times:
                    tmp_str = tmp_str + str(f_time) + " "  
                self.ping_times = []
                print "%s %s %s" % (self.local_node,self.remote_node, tmp_str)
        
    def process_header_line(self,parsed_line):
                
        self.output_results()
        self.local_node = parsed_line[1]
        self.remote_node = parsed_line[3]
        
    def process_data_line(self,parsed_line):
        tmp_time = float(parsed_line[0])
        tmp_status = parsed_line[1]
        tmp_uid = int(parsed_line[2])
            
        if tmp_status == 'sent':
            self.sent_uid_to_time[tmp_uid] = tmp_time
            self.time_to_sent_uid[tmp_time] = tmp_uid
        elif tmp_status == 'received':
            if tmp_uid in self.sent_uid_to_time:
                self.received_uid_in_sent_to_time[tmp_uid]= tmp_time
        else:
          print 'ERROR unknown packet direction'

  
if __name__=="__main__":
    infilename = sys.argv[1]
    parser = BruNetPingParse(infilename)
    parser.parse()
