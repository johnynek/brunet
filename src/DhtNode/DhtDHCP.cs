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
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop.DhtNode {
  /**
  <summary>The DhtDHCPLeaseController provides mechanisms to acquire IP
  Addresses in the Dht.  This is responsible for IP Address allocation,
  hostname reservation, and multicast subscribing, which is done in GetLease.
  </summary>
  */
  public class DhtDHCPServer: DHCPServer {
    /// <summary>The dht object used to stored lease information.</summary>
    protected IDht _dht;
    /// <summary>Multicast enabled.</summary>
    protected bool _multicast;
    /// <summary>Speed optimization for slow dht, current ip</summary>
    protected MemBlock _current_ip;
    /// <summary>Speed optimization for slow dht, lease quarter time.</summary>
    protected DateTime _current_quarter_lifetime;

    /**
    <summary>Creates a DhtDHCPLeaseController for a specific namespace</summary>
    <param name="dht">The dht object use to store lease information.</param>
    <param name="config">The DHCPConfig used to define the Lease
    parameters.</param>
    <param name="EnableMulticast">Defines if Multicast is to be enabled during
    the lease.</param>
    */
    public DhtDHCPServer(IDht dht, DHCPConfig config, bool EnableMulticast) :
      base(config)
    {
      _dht = dht;
      _multicast = EnableMulticast;
    }

    /**
    <summary>This provides a mechanism for a node to get a lease by using the
    Dht.  This uses Dht.Create which provides an atomic operation on the Dht,
    where this node is the first to store a value at a specific key.  The idea
    being that, this node being the first to store the IP, all nodes doing a
    lookup for that IP Address would be directed to this node.</summary>
    <remarks>Working with the Dht is a little tricky as transient errors could
    be misrepresented as a failed Create.  It is that reason why there is a 
    Renew parameter. If that is set, the algorithm for obtaining an address
    is slightly changed with more weight on reobtaining the RequestedAddr.
    </remarks>
    <param name="RequestedAddr">Optional parameter if the node would like to
    request a specific address.</param>
    <param name="Renew">Is the RequestedAddr a renewal?</param>
    <param name="node_address">The Brunet.Address where the DhtIpopNode resides
    </param>
    <param name="para">Optional, position 0 should hold the hostname.</param>
    */
    public override byte[] RequestLease(byte[] RequestedAddr, bool Renew,
                                       string node_address, params object[] para) {
      int max_renew_attempts = 1;
      int renew_attempts = max_renew_attempts;
      int attempts = 2;

      if(Renew) {
        MemBlock request_addr = MemBlock.Reference(RequestedAddr);
        renew_attempts = 2;
        attempts = 1;
        if(request_addr.Equals(_current_ip) && DateTime.UtcNow < _current_quarter_lifetime) {
          return _current_ip;
        }
      } else if(RequestedAddr == null || !ValidIP(RequestedAddr)) {
        RequestedAddr = MemBlock.Reference(RandomIPAddress());
      }

      byte[] hostname = null;
      if(para[0] is string) {
        hostname = Encoding.UTF8.GetBytes(Config.Namespace + "." + (para[0] as string));
      }

      byte[] multicast_key = null;
      if(_multicast) {
        multicast_key = Encoding.UTF8.GetBytes(Config.Namespace + ".multicast.ipop");
      }

      byte[] node_addr = Encoding.UTF8.GetBytes(node_address);
      bool res = false;

      while (attempts-- > 0) {
        string str_addr = Utils.BytesToString(RequestedAddr, '.');
        ProtocolLog.WriteIf(IpopLog.DHCPLog, "Attempting to allocate IP Address:" + str_addr);

        byte[] dhcp_key = Encoding.UTF8.GetBytes("dhcp:" + Config.Namespace + ":" + str_addr);
        byte[] ip_addr = Encoding.UTF8.GetBytes(str_addr);

        while(renew_attempts-- > 0) {
          try {
            res = _dht.Create(dhcp_key, node_addr, Config.LeaseTime);

            if(hostname != null) {
              _dht.Put(hostname, ip_addr, Config.LeaseTime);
            }

            if(_multicast) {
              _dht.Put(multicast_key, node_addr, Config.LeaseTime);
            }

            _dht.Put(node_addr, dhcp_key, Config.LeaseTime);
          }
          catch {
            res = false;
          }
        }
        if(res) {
          _current_ip = MemBlock.Reference(RequestedAddr);
          _current_quarter_lifetime = DateTime.UtcNow.AddSeconds(Config.LeaseTime / 4.0); 
          break;
        }
        else {
          // Failure!  Guess a new IP address
          RequestedAddr = RandomIPAddress();
          renew_attempts = max_renew_attempts;
        }
      }

      if(!res) {
        throw new Exception("Unable to get an IP Address!");
      }

      return RequestedAddr;
    }

    public static DhtDHCPServer GetDhtDHCPServer(IDht dht, string ipop_namespace, bool enable_multicast) {
      DHCPConfig config = GetDHCPConfig(dht, ipop_namespace);
      return new DhtDHCPServer(dht, config, enable_multicast);
    }

    public static DHCPConfig GetDHCPConfig(IDht dht, string ipop_namespace) {
      byte[] ns_key = Encoding.UTF8.GetBytes("dhcp:" + ipop_namespace);
      Hashtable[] results = dht.Get(ns_key);

      if(results.Length == 0) {
        throw new Exception("Namespace does not exist: " + ipop_namespace);
      }

      string result = Encoding.UTF8.GetString((byte[]) results[0]["value"]);

      XmlSerializer serializer = new XmlSerializer(typeof(DHCPConfig));
      TextReader stringReader = new StringReader(result);
      return (DHCPConfig) serializer.Deserialize(stringReader);
    }
  }
}
