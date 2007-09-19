using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections;
using System.Security.Cryptography;
using System.Diagnostics;

using Brunet;
using Brunet.Dht;

namespace Ipop {
/**
* This class implements a route miss handler in case we cannot find
* a virtual Ip -> brunet Id mapping inside our translation table. 
*/
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
      Address addr = null;
      lock (_sync) {
        addr = (Address) _results[ip];
      }
      return addr;
    }

    public void RouteMiss(IPAddress ip) {
      lock(_sync) {
        if (!_queued.Contains(ip)) {
          Debug.WriteLine(String.Format("Routes:  Adding {0} to queue.", ip));
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
      Address addr = null;
      try {
        DhtGetResult dgr = (DhtGetResult) queue.Dequeue();
        addr = AddressParser.Parse(Encoding.UTF8.GetString((byte []) dgr.value));
        Debug.WriteLine(String.Format("Routes: Got result for {0} ::: {1}.", ip, addr));
      }
      catch {
        addr = null;
        Debug.WriteLine(String.Format("Routes: Failed for {0}.", ip));
      }

      lock(_sync) {
        if(addr != null) {
          _results[ip] = addr;
        }
        _queued.Remove(ip);
        _mapping.Remove(queue);
        queue.Close();
      }
    }
  }
}
