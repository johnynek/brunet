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

using Brunet;
using Brunet.Applications;
using Brunet.DistributedServices;
using Ipop;
using System;
using System.Net;
using System.Text;
using System.Collections;

namespace Ipop.DhtNode {
  /**
  <summary>This class provides a method to do address resolution over the
  Brunet Dht, where entries are listed in the dht by means of the 
  DhtDHCPServer.</summary>
  <remarks>Entries are stored in a cache, so that frequented requests
  aren't held up by Dht access.  The dht holds the information via key = ip 
  address and value = brunet address.  Where the IP is stored as 
  dhcp:ipop_namespace:\$ipop_namespace:ip:\$ip and the Address is the
  Brunet.Address.ToString().</remarks>
  */
  public class DhtAddressResolver: IAddressResolver {
    /// <summary>A lock synchronizer for the hashtables and cache.</summary>
    protected readonly Object _sync = new Object();
    /// <summary>Holds up to 250 IP:Brunet Address translations.</summary>
    protected readonly Cache _results = new Cache(1024);
    /// <summary>Contains which IP Address Misses are pending.</summary>
    protected readonly Hashtable _queued = new Hashtable();
    /// <summary>Maps the Channel in MissCallback to an IP.</summary>
    protected readonly Hashtable _mapping = new Hashtable();
    /// <summary>The dht object to use for dht interactions.</summary>
    protected readonly IDht _dht;
    /// <summary>The ipop namespace where the dhcp server is storing names</summary>
    protected readonly string _ipop_namespace;

    /**
    <summary>Creates a DhtAddressResolver Object.</summary>
    <param name="dht">The dht object to use for dht interactions.</param>
    <param name="ipop_namespace">The ipop namespace where the dhcp server
    is storing names.</param>
    */
    public DhtAddressResolver(IDht dht, string ipop_namespace) {
      _dht = dht;
      _ipop_namespace = ipop_namespace;
    }

    /// <summary>Translates an IP Address to a Brunet Address.  If it is in the
    /// cache it returns a result, otherwise it returns null and begins a Miss
    /// lookup.</summary>
    /// <param name="ip">The IP Address to translate.</param>
    /// <returns>Null if none exists or there is a miss or the Brunet Address if
    /// one exists in the cache</returns>
    public Address Resolve(MemBlock ip) {
      Address addr = null;
      lock (_sync) {
        addr = (Address) _results[ip];
      }
      if(addr == null) {
        Miss(ip);
      }
      return addr;
    }

    /// <summary>Takes an IP and initiates resolution, i.e. async.</summary>
    /// <param name="ip"> the MemBlock representation of the IP</param>
    public void StartResolve(MemBlock ip) {
      Resolve(ip);
    }

    public bool Check(MemBlock ip, Address addr) {
      if(addr.Equals(_results[ip])) {
        return true;
      }

      Miss(ip);
      return false;
    }

    /**
    <summary>This is called if the cache's don't have an Address mapping.  It
    prepares an asynchronous Dht query if one doesn't already exist, that is
    only one query at a time per IP regardless of how many misses occur.  The
    ansychonorous call back is call MissCallback.</summary>
    <param name="ip">The IP Address to look up in the Dht.</param>
    */
    protected void Miss(MemBlock ip) {
      Channel queue = null;

      lock(_sync) {
        if (_queued.Contains(ip)) {
          return;
        }

        _queued[ip] = true;

        queue = new Channel();
        queue.CloseEvent += MissCallback;
        _mapping[queue] = ip;
      }

      String ips = Utils.MemBlockToString(ip, '.');

      ProtocolLog.WriteIf(IpopLog.ResolverLog, String.Format( "Adding {0} to queue.", ips));
      /*
      * If we were already looking up this string, there
      * would be a table entry, since there is not, start a
      * new lookup
      */

      byte[] key = Encoding.UTF8.GetBytes("dhcp:" + _ipop_namespace + ":" + ips);
      try {
        _dht.AsyncGet(key, queue);
      } catch {
        queue.CloseEvent -= MissCallback;
        lock(_sync) {
          _queued.Remove(ip);
          _mapping.Remove(queue);
        }
        queue.Close();
      }
    }

    /**
    <summary>This is the asynchronous callback for Miss.  This contains the
    lookup results from the Dht.  If there is a valid mapping it is added to
    the cache.  Either way, new queries can now be run for the IP address
    after the completion of this method.</summary>
    <param name="o">Contains the Channel where the results are stored.</param>
    <param name="args">Null.</param>
    */
    protected void MissCallback(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      MemBlock ip = (MemBlock) _mapping[queue];
      String ips = Utils.MemBlockToString(ip, '.');
      Address addr = null;

      try {
        Hashtable dgr = (Hashtable) queue.Dequeue();
        addr = AddressParser.Parse(Encoding.UTF8.GetString((byte[]) dgr["value"]));
        if(IpopLog.ResolverLog.Enabled) {
          ProtocolLog.Write(IpopLog.ResolverLog, String.Format(
            "Got result for {0} ::: {1}.", ips, addr));
        }
      }
      catch {
        if(IpopLog.ResolverLog.Enabled) {
          ProtocolLog.Write(IpopLog.ResolverLog, String.Format(
            "Failed for {0}.", Utils.MemBlockToString(ip, '.')));
        }
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
              "ERROR: In Resolves unable to remove entry: {0}\n\t{1]", ips, e));
      }
    }
  }
}
