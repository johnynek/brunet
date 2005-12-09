/**This class contains the per IP information like:
   1. when was last B-ARP sent for address
   2. queue of pointers into the OutIPBuffer
   3.  
**/
using System.Collections;
using System;
namespace Ipop {
  public class IPInformation {
    readonly static int BARP_INTERVAL = 2;
    readonly static int DROP_THRESHOLD = 10;
    
    public IPAddress addr;
    //outgoing IP packets for; a rush buffer
    Queue queue = new Queue();
    //last B-ARP 
    int lastBARP;
    
    //last sent
    int lastSent;
    //get the last sent time
    //if this time exceeds a threshold; we can discard all such packets
    
    public IPInformation(IPAddress a) {
      addr = a;
      lastBARP = -1;
    }
    public void AddPacket(byte[] packet) {
      queue.Enqueue(packet);
    }
    public Queue EmptyPacketQueue() {
      Queue ret = queue;
      queue = new Queue();
      return ret;
    }
    
  }
  public class IPInformationTable {
    //can accomodate at most 20 packets, should rather be byte-based
    private readonly static int RUSH_BUFFER_SIZE = 20;
    //a hashtable would be nice
    private ArrayList ipTable = new ArrayList();
    private int rushBufferSize = 0;
    //number of elements currently in rush buffer
    //we also need some synchronization
    public bool AddPacket(IPAddress addr, byte[] packet) {
      lock(this) {
	if (rushBufferSize == RUSH_BUFFER_SIZE) {
	  return false;
	}
	IEnumerator ie = ipTable.GetEnumerator();
	ie.Reset();
	bool done = true;
	while(ie.MoveNext()) {
	  IPInformation info = (IPInformation) ie.Current;
	  //Console.WriteLine(info.addr + ":" + addr);
	  if (info.addr.Equals(addr)) {
	    info.AddPacket(packet);
	    rushBufferSize++;
	    return true;
	  }
	}
	Console.WriteLine("New IP entry");
	//finally add new IP information
	IPInformation ipInfo = new IPInformation(addr);
	ipInfo.AddPacket(packet);
	ipTable.Add(ipInfo);
	rushBufferSize++;
	return true;
      }
    }
    //removes all the packets for a given IP address
    public Queue RemovePackets(IPAddress addr) {
      lock(this) {
	IEnumerator ie = ipTable.GetEnumerator();
	ie.Reset();
	while(ie.MoveNext()) {
	  IPInformation info = (IPInformation) ie.Current;
	  if (info.addr.Equals(addr)) {
	    Queue pQueue = info.EmptyPacketQueue();
	    rushBufferSize -= pQueue.Count;
	    ipTable.Remove(info);
	    return pQueue;
	  }	
	}
	return null;
      }
    }
  }
}
