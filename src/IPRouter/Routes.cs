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
    protected object _sync = new object();
    // Create a cache with room for 250 entries - I can't imagine having more nodes than this...
    private Cache _results = new Cache(250);
    private ArrayList _state = new ArrayList();
    private Hashtable _queued = new Hashtable(), _mapping = new Hashtable();
    private Dht _dht;
    private string _ipop_namespace;
    public Thread routes_thread;
    private BlockingQueue _command_queue = new BlockingQueue();

    public Routes(Dht dht, string ipop_namespace) {
      this._dht = dht;
      _ipop_namespace = ipop_namespace;
      routes_thread = new Thread(this.Run);
      _state.Add(_command_queue);
      routes_thread.Start();
    }

    public void Run() {
      while(true) {
        int idx = BlockingQueue.Select(_state, Timeout.Infinite);
        if(idx == -1) {
          continue;
        }
        else if(idx == 0) {
          BlockingQueue command = (BlockingQueue) _command_queue.Dequeue();
          _state.Add(command);
        }
        else {
          BlockingQueue selected_queue = (BlockingQueue) _state[idx];
          IPAddress ip = (IPAddress) _mapping[selected_queue];
          // Exception on empty
          try {
            DhtGetResult dgr = (DhtGetResult) selected_queue.Dequeue();
            lock(_sync) {
              _results[ip] = dgr.value;
            }
          }
          catch {;}
          _state.RemoveAt(idx);
          _queued.Remove(ip);
          _mapping.Remove(selected_queue);
          selected_queue.Close();
        }
      }
    }

    public Address GetAddress(IPAddress ip) {
      lock(_sync) {
        byte[] buf =  (byte[]) _results[ip];
        if(null != buf) {
          return new AHAddress( MemBlock.Reference(buf) );
        }
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
            BlockingQueue command = _dht.AsGet(key);
            _command_queue.Enqueue(command);
            _queued[ip] = true;
            _mapping[command] = ip;
          }
          catch { return; }
        }
      }
    }
  }
}
