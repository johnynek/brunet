using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections;
using System.Security.Cryptography;

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
          Console.WriteLine("naddr: " + addr.ToString());
          _tab[addr] = 1;
        }
        else {
          Console.WriteLine("iaddr: " + addr.ToString());
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
          Console.WriteLine("best_addr: " + iter.Key.ToString());
          int count = (int) iter.Value;
          if (count > max) {
            max = count;
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
      if (false == _route_miss_result_table.ContainsKey(ip)) {
        /*
          * If we were already looking up this IPAddress, there
          * would be a table entry, since there is not, start a
          * new lookup
          */
        _route_miss_result_table[ip] = new RouteMissResult(ip);
        ThreadPool.QueueUserWorkItem(new WaitCallback(this.BrunetARPHandler), ip);
      }
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
      string str_key = "dhcp:ipop_namespace:" + _ipop_namespace + ":ip:" + ip.ToString();	
      byte[] dht_key = Encoding.UTF8.GetBytes(str_key);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      dht_key = algo.ComputeHash(dht_key);

      BlockingQueue[] q = _dht.GetF(dht_key, 1000, null);
      //Don't let the table change while we're doing this:
      lock(_sync) {
        for (int i = 0; i < q.Length; i++) {
          _queue_to_ip[q[i]] =  ip;
        }
      }
      BlockingQueue.ParallelFetchWithTimeout(q, 1000, new BlockingQueue.FetchDelegate(RouteMissFetch));
    } catch(Exception) {
      /*
        * Oh well, something bad happened...
        */
    }
    finally {
      //No matter what, clear the table entry:
      lock( _sync ) {
        _route_miss_result_table.Remove(ip);
      }
    }
  }

  protected ArrayList RouteMissFetch(BlockingQueue q, int max_replies) {

    IPAddress ip;
    RouteMissResult route_miss_result;
    lock( _sync ) {
      //Get the IP address for this queue and remove it from the hashtable:
      ip = (IPAddress) _queue_to_ip[q];
      _queue_to_ip.Remove(q);
      route_miss_result = (RouteMissResult) _route_miss_result_table[ip];
    }

    ArrayList replies = new ArrayList();
    while (max_replies > 0) {
      try{
        RpcResult res = (RpcResult)q.Dequeue();
        replies.Add(res);
        ArrayList result = (ArrayList) res.Result;
        if (result == null || result.Count < 3) {
          /*
            * What the hell is this case??  Someone should
            * explain why we are doing this.  It appears
            * to be for error checking the result, but it's
            * not clear.
            */
          continue;
        }
        ArrayList values = (ArrayList) result[0];

        foreach (Hashtable ht in values) {
          byte[] data = (byte[]) ht["value"];
          Address addr = new AHAddress(data);
          //now place all these in the result queue
          route_miss_result.AddResult(addr);
        }

        //now determine the best address to use
        Address best_addr = route_miss_result.GetBestResult();
        if (best_addr != null) {
          _route_miss_delegate(ip, best_addr);
        }
        max_replies--;
      }
      catch (Exception) {
        break;
      }
    }
    //this is where we get rid of the queue
    return replies;
  }
}
}
