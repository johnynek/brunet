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
using Brunet.Collections;
using Brunet.Concurrent;
using Brunet.Services.Dht;
using Brunet.Util;
using Ipop;
using System;
using System.Net;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Ipop.Dht {
  /// <summary>This class provides a method to do address resolution over the
  /// Brunet Dht, where entries are listed in the dht by means of the 
  /// DhtDHCPServer.</summary>
  /// <remarks>Entries are stored in a Dictionary (_results) that gets cleared
  /// every 10 minutes.  After the 10 minute mark, the results are pushed into
  /// a single stage garbage collection (_last_results).  If _results has a 
  /// miss but _last_results has a hit, the result is returned but is also
  /// queried into the Dht.</remarks>
  public class DhtAddressResolver: IAddressResolver {
    /// <summary>Clean up the cache entries every 10 minutes.</summary>
    public const int CLEANUP_TIME_MS = 600000;
    /// <summary>A lock synchronizer for the hashtables and cache.</summary>
    protected readonly Object _sync = new Object();
    /// <summary>Contains IP::Address mappings.</summary>
    protected Dictionary<MemBlock, Address> _results;
    /// <summary>_results pre-garbage collection cache.</summary>
    protected Dictionary<MemBlock, Address> _last_results;
    /// <summary>Failed query attempts.</summary>
    protected readonly Dictionary<MemBlock, int> _attempts;
    /// <summary>Contains which IP Address Misses are pending.</summary>
    protected readonly Dictionary<MemBlock, bool> _queued;
    /// <summary>Maps the Channel in MissCallback to an IP.</summary>
    protected readonly Dictionary<Channel, MemBlock> _mapping;
    /// <summary>The dht object to use for dht interactions.</summary>
    protected readonly IDht _dht;
    /// <summary>The ipop namespace where the dhcp server is storing names</summary>
    protected readonly string _ipop_namespace;
    /// <summary>Timer that handles the garbage collection of mappings.</summary>
    protected readonly FuzzyEvent _fe;
    /// <summary>The DhtAddressResolver is done, if _stopped == 1.</summary>
    protected int _stopped;

    /// <summary>Creates a DhtAddressResolver Object.</summary>
    /// <param name="dht">The dht object to use for dht interactions.</param>
    /// <param name="ipop_namespace">The ipop namespace where the dhcp server
    /// is storing names.</param>
    public DhtAddressResolver(IDht dht, string ipop_namespace)
    {
      _dht = dht;
      _ipop_namespace = ipop_namespace;

      _last_results = new Dictionary<MemBlock, Address>();
      _results = new Dictionary<MemBlock, Address>();
      _attempts = new Dictionary<MemBlock, int>();
      _queued = new Dictionary<MemBlock, bool>();
      _mapping = new Dictionary<Channel, MemBlock>();
      _fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(CleanUp, CLEANUP_TIME_MS, CLEANUP_TIME_MS / 10);
      _stopped = 0;
    }

    protected void CleanUp(DateTime now)
    {
      lock(_sync) {
        _last_results = _results;
        _results = new Dictionary<MemBlock, Address>();
      }
    }

    /// <summary>Translates an IP Address to a Brunet Address.  If it is in the
    /// cache it returns a result, otherwise it returns null and begins a Miss
    /// lookup.</summary>
    /// <param name="ip">The IP Address to translate.</param>
    /// <returns>Null if none exists or there is a miss or the Brunet Address if
    /// one exists in the cache</returns>
    public Address Resolve(MemBlock ip)
    {
      Address addr = null;
      lock(_sync) {
        if(_results.TryGetValue(ip, out addr)) {
          return addr;
        }
        _last_results.TryGetValue(ip, out addr);
      }
      Miss(ip);
      return addr;
    }

    /// <summary>Is the right person sending me this packet?</summary>
    /// <param name="ip">The IP source.</param>
    /// <param name="addr">The Brunet.Address source.</summary>
    public bool Check(MemBlock ip, Address addr)
    {
      // Check current results
      Address stored_addr = null;
      lock(_sync) {
        _results.TryGetValue(ip, out stored_addr);
      }

      if(addr.Equals(stored_addr)) {
        // Match!
        return true;
      } else if(stored_addr == null) {
        // No entry, check previous contents
        lock(_sync) {
          _last_results.TryGetValue(ip, out stored_addr);
        }
        if(Miss(ip)) {
          IncrementMisses(ip);
        }
        return addr.Equals(stored_addr);
      } else {
        // Bad mapping
        Miss(ip);
        throw new AddressResolutionException(String.Format(
              "IP:Address mismatch, expected: {0}, got: {1}",
              addr, stored_addr), AddressResolutionException.Issues.Mismatch);
      }
    }

    protected void IncrementMisses(MemBlock ip)
    {
      lock(_attempts) {
        int count = 1;
        if(_attempts.ContainsKey(ip)) {
          count =  _attempts[ip] + 1;
        }
        _attempts[ip] = count;

        if(count >= 3) {
          _attempts.Remove(ip);
          throw new AddressResolutionException("No Address mapped to: " + Utils.MemBlockToString(ip, '.'),
              AddressResolutionException.Issues.DoesNotExist);
        }
      }
    }

    /// <summary>This is called if the cache's don't have an Address mapping.
    /// It prepares an asynchronous Dht query if one doesn't already exist,
    /// that is only one query at a time per IP regardless of how many misses
    /// occur.  The ansychonorous call back is call MissCallback.</summary>
    /// <param name="ip">The IP Address to look up in the Dht.</param>
    protected bool Miss(MemBlock ip)
    {
      Channel queue = null;

      lock(_sync) {
        // Already looking up or found
        if(_queued.ContainsKey(ip) || _results.ContainsKey(ip)) {
          return false;
        }

        _queued[ip] = true;

        queue = new Channel(1);
        queue.CloseEvent += MissCallback;
        _mapping[queue] = ip;
      }

      String ips = Utils.MemBlockToString(ip, '.');
      ProtocolLog.WriteIf(IpopLog.ResolverLog, String.Format( "Adding {0} to queue.", ips));

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

      return true;
    }

    /// <summary>This is the asynchronous callback for Miss.  This contains the
    /// lookup results from the Dht.  If there is a valid mapping it is added to
    /// the cache.  Either way, new queries can now be run for the IP address
    /// after the completion of this method.</summary>
    /// <param name="o">Contains the Channel where the results are stored.</param>
    /// <param name="args">Null.</param>
    protected void MissCallback(Object o, EventArgs args)
    {
      Channel queue = (Channel) o;
      // Requires synchronized reading
      MemBlock ip = null;
      lock(_sync) {
        ip = _mapping[queue];
      }
      String ips = Utils.MemBlockToString(ip, '.');
      Address addr = null;

      try {
        Hashtable dgr = (Hashtable) queue.Dequeue();
        addr = AddressParser.Parse(Encoding.UTF8.GetString((byte[]) dgr["value"]));
        if(IpopLog.ResolverLog.Enabled) {
          ProtocolLog.Write(IpopLog.ResolverLog, String.Format(
            "Got result for {0} ::: {1}.", ips, addr));
        }
      } catch {
        if(IpopLog.ResolverLog.Enabled) {
          ProtocolLog.Write(IpopLog.ResolverLog, String.Format(
            "Failed for {0}.", Utils.MemBlockToString(ip, '.')));
        }
      }

      lock(_sync) {
        if(addr != null) {
          _results[ip] = addr;
          _attempts.Remove(ip);
        }

        _queued.Remove(ip);
        _mapping.Remove(queue);
      }
    }

    /// <summary>Stops the timer at which point a new DhtAddressResolver
    /// will need to be constructed, if used in the same process.</summary>
    public void Stop()
    {
      if(System.Threading.Interlocked.Exchange(ref _stopped, 1) == 0) {
        return;
      }

      _fe.TryCancel();
    }
  }
}
