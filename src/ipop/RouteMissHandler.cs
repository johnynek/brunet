#define ROUTE_MISS_DEBUG

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  /**
   * This class implements a route miss handler in case we cannot find
   * a virtual Ip -> brunet Id mapping inside our translation table. 
   * NOTE: This has to be thread safe for sure.
   **/
  public class RouteMissHandler {
    class RouteMissResult {
      protected object _sync;
      protected Hashtable _tab;
      protected IPAddress _ip;
      public IPAddress IPAddress { get { return _ip; } }

      public void AddResult(Address addr) {
	lock(_sync) {
	if (!_tab.ContainsKey(addr)) {
	  _tab[addr] = 1;
	} else {
	  int curr = (int) _tab[addr];
	  _tab[addr] = curr + 1;
	}
	}
      }
      /** 
       * @return IPAddress that is in majority
       */
      public Address GetBestResult() {
	lock(_sync) {
	  int max = -1;
	  Address best_addr = null;
	  IDictionaryEnumerator iter = _tab.GetEnumerator();
	  while(iter.MoveNext()) {
	    int count = (int) iter.Value;
	    if (count > max) {
	      best_addr = (Address) iter.Key; 
	    }
	  }
	  return best_addr;
	}
      }
      public RouteMissResult(IPAddress ip) {
	_sync = new object();
	_ip = ip;
	_tab = new Hashtable();
      }
    }
    
    /** lock object. */
    object _sync;

    public delegate void RouteMissDelegate(IPAddress ip, Address addr);
    
    //invoked everytime we get a Get() result
    private RouteMissDelegate _route_miss_delegate;

    //handle to distributed hash table
    private FDht _dht = null;

    /** keep track of outstanding Brunet-ARP operations. */
    private Hashtable _route_miss_result_table = null;
    private Hashtable _queue_to_ip = null;
    
    //ipop namesapce
    private string _ipop_namespace;
    
    /** Constructor.
     **/
    public RouteMissHandler(FDht dht, string ipop_namespace, RouteMissDelegate dlgt) {
      _sync = new object();
      _dht = dht;
      _ipop_namespace = ipop_namespace;

      _route_miss_delegate = dlgt;

      _route_miss_result_table = new Hashtable();
      _queue_to_ip = new Hashtable();
    }
    
    public void HandleRouteMiss(IPAddress ip) {
      lock(_sync) {
	if (_route_miss_result_table.ContainsKey(ip)) {
#if ROUTE_MISS_DEBUG
	  Console.WriteLine("Outstanding Brunet-ARP() for IP: {0}, don't do another Get()", ip);
#endif
	  return;
	}
#if ROUTE_MISS_DEBUG
	Console.WriteLine("Executing Brunet-ARP() for IP: {0}", ip);
#endif
	_route_miss_result_table[ip] = new RouteMissResult(ip);
	ThreadPool.QueueUserWorkItem(new WaitCallback(this.BrunetARPHandler), (object) ip);
      }
    }
    
    /** 
     * The following method is invoked everytime something is placed into the queue
     * The entire code should be thread safe.
     */
    protected void BrunetARPHandler(object o) 
    {
      IPAddress ip = o as IPAddress;
      try {
        string str_key = "dhcp:ip:" + ip.ToString();
	
	byte[] dht_key = Encoding.UTF8.GetBytes(str_key);
#if ROUTE_MISS_DEBUG
	Console.WriteLine("Invoking get() on: {0}", str_key);
#endif
	BlockingQueue[] q = _dht.GetF(dht_key, 1000, null);
	for (int i = 0; i < q.Length; i++) {
	  _queue_to_ip[q[i]] =  ip;
	}
	BlockingQueue.ParallelFetchWithTimeout(q, 10000, new BlockingQueue.FetchDelegate(RouteMissFetch));

#if ROUTE_MISS_DEBUG
	Console.WriteLine("Finishing Brunet-ARP for ip: {0}", ip);
#endif
      
	//we are now done
	_route_miss_result_table.Remove(ip);
      } catch(Exception) {
	//in case of the exception too, clear thhe Brunet-ARP entry
	_route_miss_result_table.Remove(ip);
      }
    }
    
    protected ArrayList RouteMissFetch(BlockingQueue q, int max_replies) {

      ArrayList replies = new ArrayList();
      IPAddress ip = (IPAddress) _queue_to_ip[q];
      RouteMissResult route_miss_result = (RouteMissResult) _route_miss_result_table[ip];

      while (true) {
	try{
	  if (max_replies == 0) {
	    break;
	  }
	  RpcResult res = q.Dequeue() as RpcResult;
	  replies.Add(res);
	  ArrayList result = (ArrayList) res.Result;
	  if (result == null || result.Count < 3) {
	    continue;
	  }
	  ArrayList values = (ArrayList) result[0];
#if ROUTE_MISS_DEBUG
	  Console.WriteLine("# of matching entries: " + values.Count);
#endif
	
	  foreach (Hashtable ht in values) {
	    byte[] data = (byte[]) ht["data"];
	    Address addr = new AHAddress(data);
	    
	    //now place all these in the result queue
	    route_miss_result.AddResult(addr);
	  }
	
	  //now determine the best address to use
	  Address best_addr = route_miss_result.GetBestResult();
	  if (best_addr != null) {
#if ROUTE_MISS_DEBUG
	    Console.WriteLine("Current best estimate for ip: {0} is brunet id: {1}", ip, best_addr);
#endif
	    _route_miss_delegate(ip, best_addr);
	  }
	  max_replies--;
	} catch (InvalidOperationException) {
	  break;
	}
      }
      //this is where we get rid of the queue
      _queue_to_ip.Remove(q);
      return replies;
    }
  }
}
