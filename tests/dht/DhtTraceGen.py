#!/usr/bin/python

#use this script to generate a trace program for Dht
import random
import sys
class Entry:
    def __init__(self,key, data, passwd):
        self.key = key
        self.data = data
        self.passwd = passwd

entry_list = []
event_list = []
class Event:
    def __init__(self,time):
        self.time = time;
    def fire(self):
        pass
class StartEvent(Event):
    def __init__(self,time):
        self.time = time
    def fire(self):
        print "Start"

class KillEvent(Event):
    def __init__(self,time):
        self.time = time
    def fire(self):
        print "Kill"

class CreateEvent(Event):
    def __init__(self,time,entry):
        self.time = time
        self.entry = entry
    def fire(self):
        print "Create"
        print self.entry.key
        print self.entry.data
        print self.entry.passwd
        print 100000

class DeleteEvent(Event):
    def __init__(self,time,entry):
        self.time = time
        self.entry = entry
    def fire(self):
        print "Delete"
        print self.entry.key
        print self.entry.passwd

class GetEvent(Event):
    def __init__(self,time,entry):
        self.time = time
        self.entry = entry
    def fire(self):
        print "Check"
        print self.entry.key
class GlobalCheckEvent(Event):
    def __init__(self,time):
        self.time = time
    def fire(self):
        print "Global_Check"    

def add_to_list(e):
    k  = 0
    for i in range(0, len(event_list)):
        c_event = event_list[i]
        if (e.time < c_event.time):
            break;
        else:
            k = k + 1
    event_list.insert(k, e)            
            
max_time = 360.0
pre_start = 0
growth = 0
if len(sys.argv) > 1:
    max_time = int(sys.argv[1])
if len(sys.argv) > 2:
    pre_start = int(sys.argv[2])
if len(sys.argv) > 3:
    growth = int(sys.argv[3])    
#60 second arrival
node_birth_rate = 1.0/(60)
#60 second departure
node_death_rate = 1.0/(60)

key_birth_rate = 1.0/(30)
key_death_rate = 1.0/(30)

#now generate a schedule for node arrival
time = 0.0

while time < max_time:
    time = time + random.expovariate(node_birth_rate)
    e = StartEvent(time)
    add_to_list(e)

#now generate a schedule for node departure
time = 0.0
while time < max_time:
    time = time + random.expovariate(node_birth_rate)
    e = KillEvent(time)
    add_to_list(e)

#now schedule creation of keys
time = 0.0
idx = 0
while time < max_time:
    add_time = time + random.expovariate(key_birth_rate)
    entry = Entry(idx, "%d"%idx , "abc123")
    e = CreateEvent(add_time, entry)
    add_to_list(e)
    #wewill also have to deceide on delete time for the key
    del_time = add_time + random.expovariate(key_birth_rate)
    e = DeleteEvent(del_time, entry)
    add_to_list(e)

    #we also add a Get() for the same key sometime (1.0 seconds).
    get_time = del_time + 1.0
    e = GetEvent(get_time, entry);
    add_to_list(e)

    time = add_time
    idx = idx + 1
    

#lets start some nodes articifially
#these nodes that we start will also do Creates on the way
for i in range(0, pre_start):
    print "Start"
    print "Sleep"
    print "5000"

print "Sleep"
print "100000"
  
for i in range(0, growth):
    print "Global_Check"
 
    print "Start"

    print "Sleep"
    print "20000"

    print "Create"
    print "growth_%d"%i
    print "growth_%d"%i
    print "abc123"
    print "100000"
    
    print "Sleep"
    print "20000"
    
    

print "Sleep"
print "100000"

#finally schedule the arrival events
for i in range(0, len(event_list)):
    print "Global_Check"
    event = event_list[i]
    event.fire()
    print "Sleep"
    if i < len(event_list) - 1:
        n_event = event_list[i+1]
        print (n_event.time - event.time)*1000
    else:
        print -1.0


    

