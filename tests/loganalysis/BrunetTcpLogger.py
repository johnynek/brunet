#!/usr/bin/env python
import asynchat, asyncore, socket, select, os,urllib, urlparse,cStringIO
import sys,re, threading
from datetime import timedelta, datetime

class request_handler(asynchat.async_chat):
    def __init__(self,conn,addr,server):
        asynchat.async_chat.__init__(self,conn)
        self.client_address=addr
        self.connection=conn
        self.server=server
        self.buffer = ""
        self.set_terminator("\n")
        self.regexp = re.compile('(\w*)_(\d*)')
        self.client_brunet_address = -1
        print "Connect: " , self.client_address
    def collect_incoming_data(self,data):
        #Collects the incoming data
        self.buffer = self.buffer + data
    def process_log_line(self,logtype,logversion,split_line):
        if logtype in self.server.log_protocols:
            #print split_line
            self.client_brunet_address = int(split_line[4])
            #self.server.last_delete_time[r_a] = None
            self.server.log_protocols[logtype].process_line(logversion,split_line)
        else:
            print "ERROR: unknown protocol"
    def found_terminator(self):
        tmp_list = []
        init_parsed_line = self.buffer.split()
        logtype = "UNDEFINED"
        logversion = -1
        match = self.regexp.match(init_parsed_line[0])
        if match:
          logtype = match.group(1)
          logversion = int( match.group(2) )
        else:
          print "NO MATCH"
        split_line = []
        if self.server.use_local_times is True:
          tmp_time = datetime.utcnow()
          split_line.append(tmp_time.strftime("%m/%d/%Y"))
          time_str = "%s%i" % ( tmp_time.strftime("%T:") ,
          int(tmp_time.microsecond/1000) )
          split_line.append(time_str)
          for loop in init_parsed_line[3:len(init_parsed_line)]:
            split_line.append(loop)
        else:
          split_line = init_parsed_line[1:len(init_parsed_line)]
           
        self.process_log_line(logtype,logversion,split_line)
        self.buffer = "" 
        
    def handle_close(self):
        print "Disconnect: " , self.client_address
        current_dt = datetime.utcnow()
        init_parsed_line = []
        init_parsed_line.append("con_0")
        tmp_time = datetime.utcnow()
        init_parsed_line.append(tmp_time.strftime("%m/%d/%Y"))
        time_str = "%s%i" % ( tmp_time.strftime("%T:") ,
        int(tmp_time.microsecond/1000) )
        init_parsed_line.append(time_str)
        init_parsed_line.append("deletion")
        init_parsed_line.append("deletion")
        init_parsed_line.append(str(self.client_brunet_address))
        init_parsed_line.append(str(self.client_brunet_address))
        init_parsed_line.append("deletion")
        
        print init_parsed_line
        
        logtype = "UNDEFINED"
        logversion = -1
        match = self.regexp.match(init_parsed_line[0])
        if match:
            logtype = match.group(1)
            logversion = int( match.group(2) )
        else:
            print "NO MATCH"
        split_line = init_parsed_line[1:len(init_parsed_line)]  
        self.process_log_line(logtype,logversion,split_line)
        
        self.connection.close()
        self.close()
class Server(asyncore.dispatcher):
    def __init__ (self, ip, port,handler,log_protocols,use_local_times):
        self.ip = ip
        self.port = port
        self.handler=handler
        asyncore.dispatcher.__init__ (self)
        self.create_socket (socket.AF_INET, socket.SOCK_STREAM)
        self.set_reuse_addr()
        self.bind ((ip, port))
        self.listen (1024)
        self.log_dictionary = {}
        self.log_protocols = log_protocols
        self.use_local_times = use_local_times
        # node address --> last log time for address
        #self.last_log_time = {}
        # node address --> last time address was deleted
        #self.last_delete_time = {}
    def handle_accept (self):
        try:
            conn, addr = self.accept()
        except socket.error:
            print socket.error
            return
        except TypeError:
            print TypeError
            return
        # creates an instance of the handler class to handle the request/response
        # on the incoming connection
        self.handler(conn,addr,self)
        
# base class of all logging protocols        
class BrunetLogProtocol:
    def __init__(self):
        self.type = None
        self.versions = {}
    def process_line(self,parsed_line):
        pass
# log protocol for acquiring Brunet Connection logs
# this is currently the only protocol that we need to log
# over the Network.  All versions of the protocol are
#stored in this class
class ConnectionLogProtocol(BrunetLogProtocol):
    def __init__(self,log_interval):
        BrunetLogProtocol.__init__(self)
        BrunetLogProtocol.type = "con"
        tmp_version = ConnectionLogV0(log_interval)
        self.versions.setdefault(0,tmp_version)
    def process_line(self,version,parsed_line):
        if version in self.versions:
            self.versions[version].process_line(parsed_line)
        else:
            print "ERROR: unknown version"
# converts the date/time format in the connection log into python datetime
# objects
class ParseBrunetTime:
    def __init__(self,tmp_date,tmp_time):
        p_d = tmp_date.split('/')
        p_t = tmp_time.split(':')
        year = int(p_d[2])
        month = int(p_d[0])
        day = int(p_d[1])
        hour = int(p_t[0])
        minute = int(p_t[1])
        second = int(p_t[2])
        microsecond = 1000*int(p_t[3])
        self.timestamp = datetime(year,month,day,hour,minute,second,microsecond)
    def to_datetime(self):
        return self.timestamp
# the first version (V0) of the Connection log format.
# future versions can be accomodated by adding additional
# versions to the version dictionary in the __main__ loop
class ConnectionLogV0:
    def __init__(self,log_interval):
        self.log_dictionary = {}
        self.interval = log_interval
        self.total_time = 0 +  self.interval
        self.write_timer = None 
        self.first_dump = True
        self.first_run = True
        self.file_name = "confile.txt.tmp"
        self.map_file_name = "mapconfile.txt.tmp"
        self.file_name_new = "confile.txt"
        self.map_file_name_new = "mapconfile.txt"
        self.con_file = None
        self.map_con_file = None
        self.last_dump_time = datetime.now()
        self.last_process_time = None
        self.bigints = {}
    def process_line(self,parsed_line):
        self.last_process_time = datetime.now()
        if self.first_run is True:
          self.write_timer = threading.Timer(self.interval,self.dump_results)
          self.first_run = False
          self.write_timer.start()
        pbt = ParseBrunetTime(parsed_line[0],parsed_line[1])
        current_dt = pbt.to_datetime()
        tmp_list = []
        ConHashLock.acquire()
        if current_dt in self.log_dictionary:
          tmp_list = self.log_dictionary[current_dt]
          tmp_list.append(parsed_line)
          self.log_dictionary[current_dt] = tmp_list
        else:
          tmp_list.append(parsed_line)
          self.log_dictionary[current_dt] = tmp_list
        local_i = int(parsed_line[4])
        self.bigints.setdefault(local_i)
        remote_i = int(parsed_line[5])
        self.bigints.setdefault(remote_i)
        ConHashLock.release()
    def unlocked_process_line(self,parsed_line):
        self.last_process_time = datetime.now()
        if self.first_run is True:
          self.write_timer = threading.Timer(self.interval,self.dump_results)
          self.first_run = False
          self.write_timer.start()
        pbt = ParseBrunetTime(parsed_line[0],parsed_line[1])
        current_dt = pbt.to_datetime()
        tmp_list = []
        #ConHashLock.acquire()
        if current_dt in self.log_dictionary:
          tmp_list = self.log_dictionary[current_dt]
          tmp_list.append(parsed_line)
          self.log_dictionary[current_dt] = tmp_list
        else:
          tmp_list.append(parsed_line)
          self.log_dictionary[current_dt] = tmp_list
        local_i = int(parsed_line[4])
        self.bigints.setdefault(local_i)
        remote_i = int(parsed_line[5])
        self.bigints.setdefault(remote_i)
        #ConHashLock.release()
        
    def dump_results(self):
        ConHashLock.acquire()
        do_dump = False
        if self.first_dump is True:
          do_dump = True
          self.first_dump = False
        elif self.last_dump_time < self.last_process_time:
          do_dump = True
          
        if do_dump is True:
            delfile = None
            delfilename = "./manual_log_insertion.txt"
            if os.path.exists(delfilename):
                delfile = open(delfilename, 'r')
                man_del_lines = delfile.readlines()
                for d_line in man_del_lines:
                    s_line = d_line.split()
                    print "T ", s_line
                    if len(s_line) > 4:
                      self.unlocked_process_line( s_line )
                delfile.close()
                os.remove(delfilename)    
                    
            sorted_ints = self.bigints.keys()
            sorted_ints.sort()
            tmp_int = 0
            self.map_con_file = open(  self.map_file_name, 'w')
            for b_add in sorted_ints:  
                self.map_con_file.write("%i ---> %i\n" % (tmp_int , b_add ) )
                self.bigints[b_add] = tmp_int
                tmp_int = tmp_int + 1
            self.map_con_file.close()
            os.rename( self.map_file_name,self.map_file_name_new)

            sorted_keys = self.log_dictionary.keys()
            sorted_keys.sort()
            self.con_file = open(  self.file_name, 'w')
            for loop in sorted_keys:
                t_l = self.log_dictionary[loop]
                for t_l_el in t_l:
                    if len(t_l_el) > 4:
                        local_i = int(t_l_el[4])
                        local_sm = self.bigints[local_i]
                        remote_i = int(t_l_el[5])
                        remote_sm = self.bigints[remote_i]
                        if loop.microsecond == 0 :
                          tmp_str =  "%s.000000 %s %s %i %i %s" % (loop,t_l_el[2], \
                          t_l_el[3],local_sm,remote_sm,t_l_el[6] ) #, \
                          self.con_file.write("%s\n" % (tmp_str ) )
                        else:
                          tmp_str =  "%s %s %s %i %i %s" % (loop,t_l_el[2], \
                          t_l_el[3],local_sm,remote_sm,t_l_el[6] ) #, \
                          self.con_file.write("%s\n" % (tmp_str ) )
                    else:
                        print len(t_l_el)
            self.con_file.close()
            os.rename(self.file_name,self.file_name_new)
            self.last_dump_time = datetime.now()
            #self.total_time = self.interval + self.total_time
            #self.file_name = "confile.txt.tmp" % (self.total_time)
            #self.con_file = open(  self.file_name, 'w')
            #self.map_file_name = "mapconfile.txt.tmp" % (self.total_time)
            #self.map_con_file = open(  self.map_file_name, 'w')
        self.write_timer = threading.Timer(self.interval,self.dump_results)
        self.write_timer.start()
        ConHashLock.release()

ConHashLock = threading.RLock()

#global dictionary of deleted addresses
DeleteHashLock = threading.RLock()
DeleteHashTable = {}

if __name__=="__main__":
    # launch the server on port 8080
    log_interval = sys.argv[1]
    use_local_times = False
    port = 8003
    log_protocols = {}
    con_prot = ConnectionLogProtocol( int(log_interval) )
    log_protocols.setdefault("con", con_prot)
    s=Server('',port, request_handler,log_protocols,use_local_times)
    print "BruNet data logger running on port %s" % port
    asyncore.loop()
