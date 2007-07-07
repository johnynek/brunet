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
* This lacks a way to expire entries, the old method would only expire entries
* if the object had been pushed out of the stack
**/
  public class Routes {
    // Create a cache with room for 250 entries - I can't imagine having more nodes than this...
    private object _sync = new object();
    private Cache _results = new Cache(250);
    private Hashtable _queued = new Hashtable(), _mapping = new Hashtable();
    private Dht _dht;
    private string _ipop_namespace;

    public Routes(Dht dht, string ipop_namespace) {
      this._dht = dht;
      _ipop_namespace = ipop_namespace;
    }

    public Address GetAddress(IPAddress ip) {
      byte[] buf =  null;
      lock (_sync) {
        buf = (byte[]) _results[ip];
      }
      if(null != buf) {
        return new AHAddress( MemBlock.Reference(buf) );
      }
      return null;
    }

    public void RouteMiss(IPAddress ip) {
      lock(_sync) {
        if (!_queued.Contains(ip)) {
          /*
          * If we were already looking up this IPAddress, there
          * would be a table entry, since there is not, start a
          * new lookup
          */
          string key = "dhcp:ipop_namespace:" + _ipop_namespace + ":ip:" + ip.ToString();
          try {
            BlockingQueue queue = new BlockingQueue();
            queue.EnqueueEvent += RouteMissCallback;
            queue.CloseEvent += RouteMissCallback;
            _dht.AsGet(key, queue);
            _queued[ip] = true;
            _mapping[queue] = ip;
          }
          catch { return; }
        }
      }
    }

    public void RouteMissCallback(Object o, EventArgs args) {
      BlockingQueue queue = (BlockingQueue) o;
      IPAddress ip = (IPAddress) _mapping[queue];
      DhtGetResult dgr = null;
      // Exception on empty
      try {
        dgr = (DhtGetResult) queue.Dequeue();
      }
      catch {
        dgr = null;
      }

      lock(_sync) {
        _results[ip] = dgr.value;
        _queued.Remove(ip);
        _mapping.Remove(queue);
        queue.Close();
      }
    }
  }
}
