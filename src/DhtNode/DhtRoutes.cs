/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Net;
using System.Text;
using System.Collections;

using Brunet;
using Brunet.DistributedServices;

namespace Ipop {
/**
* This class implements a route miss handler in case we cannot find
* a virtual Ip -> brunet Id mapping inside our translation table. 
*/
  public class DhtRoutes: IRoutes {
    // Create a cache with room for 250 entries - I can't imagine having more nodes than this...
    private object _sync = new object();
    private Cache _results = new Cache(250);
    private Hashtable _queued = new Hashtable(), _mapping = new Hashtable();
    private Dht _dht;
    private string _ipop_namespace;

    public DhtRoutes(Dht dht, string ipop_namespace) {
      this._dht = dht;
      _ipop_namespace = ipop_namespace;
    }

    public Address GetAddress(string ip) {
      Address addr = null;
      lock (_sync) {
        addr = (Address) _results[ip];
      }
      if(addr == null) {
        RouteMiss(ip);
      }
      return addr;
    }

    protected void RouteMiss(string ip) {
      lock(_sync) {
        if (_queued.Contains(ip)) {
          return;
        }

        ProtocolLog.WriteIf(IpopLog.RoutingLog, String.Format(
          "Routes:  Adding {0} to queue.", ip));
        /*
        * If we were already looking up this string, there
        * would be a table entry, since there is not, start a
        * new lookup
        */
        string key = "dhcp:ipop_namespace:" + _ipop_namespace + ":ip:" + ip.ToString();
        Channel queue = null;
        try {
          queue = new Channel();
          queue.EnqueueEvent += RouteMissCallback;
          queue.CloseEvent += RouteMissCallback;
          _queued[ip] = true;
          _mapping[queue] = ip;
          _dht.AsGet(key, queue);
        }
        catch {
          queue.EnqueueEvent -= RouteMissCallback;
          queue.CloseEvent -= RouteMissCallback;
          if(_queued.Contains(ip)) {
            _queued.Remove(ip);
          }
          if(_mapping.Contains(queue)) {
            _mapping.Remove(queue);
          }
          queue.Close();
        }
      }
    }

    protected void RouteMissCallback(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      string ip = (string) _mapping[queue];
      Address addr = null;

      try {
        DhtGetResult dgr = (DhtGetResult) queue.Dequeue();
        addr = AddressParser.Parse(Encoding.UTF8.GetString((byte []) dgr.value));
        ProtocolLog.WriteIf(IpopLog.RoutingLog, String.Format(
          "Routes: Got result for {0} ::: {1}.", ip, addr));
      }
      catch {
        ProtocolLog.WriteIf(IpopLog.RoutingLog, String.Format(
          "Routes: Failed for {0}.", ip));
      }
      try {
        lock(_sync) {
          if(addr != null) {
            _results[ip] = addr;
          }

          _queued.Remove(ip);
          _mapping.Remove(queue);
        }
        queue.Close();
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
              "ERROR: In Routes unable to remove entry: {0}\n\t{1]", ip, e));
      }
    }
  }
}
