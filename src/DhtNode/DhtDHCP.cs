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
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Brunet;
using Brunet.DistributedServices;
using Brunet.Applications;

namespace Ipop {
  public class DhtDHCPLeaseController: DHCPLeaseController {
    protected Dht _dht;
    protected bool _multicast;

    public DhtDHCPLeaseController(Dht dht, IPOPNamespace config,
                                  bool EnableMulticast): base(config) {
      _dht = dht;
      _multicast = EnableMulticast;
    }

    public override DHCPReply GetLease(byte[] RequestedAddr, bool Renew,
                                       string node_address, params object[] para) {
      String hostname = (String) para[0];
      if(RequestedAddr == null) {
        RequestedAddr = new byte[4] {0, 0, 0, 0};
      }
      DHCPReply reply = new DHCPReply();

      int max_attempts = 1, max_renew_attempts = 2;
      if(!Renew) {
        if(RequestedAddr[0] == 0) {
          RequestedAddr = RandomIPAddress();
        }
        max_attempts = 2;
        max_renew_attempts = 1;
      }

      bool res = false;

      while (max_attempts-- > 0) {
        while(max_renew_attempts-- > 0) {
          string str_addr = Utils.BytesToString(RequestedAddr, '.');
          string key = "dhcp:ipop_namespace:" + namespace_value + ":ip:" + str_addr;
          try {
            res = _dht.Create(key, (string) node_address, leasetime);
          }
          catch {
            res = false;
          }
          if(res) {
            if(hostname != null) {
              _dht.Put(hostname + DhtDNS.SUFFIX, str_addr, leasetime);
            }
            if(_multicast) {
              _dht.Put("multicast.ipop_vpn", (string) node_address, leasetime);
            }
            _dht.Put((string) node_address, key + "|" + DateTime.Now.Ticks, leasetime);
            break;
          }
        }
        if(!res) {
          // Failure!  Guess a new IP address
          RequestedAddr = RandomIPAddress();
        }
        else {
          break;
        }
      }

      if(!res) {
        throw new Exception("Unable to get an IP Address!");
      }

      reply.ip = RequestedAddr;
      reply.netmask = netmask;
      reply.leasetime = leasetimeb;

      return reply;
    }
  }

  public class DhtDHCPServer: DHCPServer {
    protected Dht _dht;
    protected bool _multicast;

    public DhtDHCPServer(Dht dht, bool EnableMulticast) {
      _multicast = EnableMulticast;
      _dht = dht;
    }

    protected override DHCPLeaseController GetDHCPLeaseController(string ipop_namespace) {
      if (_dhcp_lease_controllers.ContainsKey(ipop_namespace)) {
        return (DHCPLeaseController) _dhcp_lease_controllers[ipop_namespace];
      }
      string ns_key = "dhcp:ipop_namespace:" + ipop_namespace;
      DhtGetResult[] results = _dht.Get(ns_key);
      if(results == null || results.Length == 0 || results[0].valueString == null)  {
        Debug.WriteLine("Namespace ({0}) does not exist", ipop_namespace);
        return null;
      }

      string xml_str = results[0].valueString.ToString();
      XmlSerializer serializer = new XmlSerializer(typeof(IPOPNamespace));
      TextReader stringReader = new StringReader(xml_str);
      IPOPNamespace ipop_ns = (IPOPNamespace) serializer.Deserialize(stringReader);
      DHCPLeaseController dhcpLeaseController = new DhtDHCPLeaseController(_dht, ipop_ns, _multicast);
      _dhcp_lease_controllers[ipop_namespace] = dhcpLeaseController;
      return dhcpLeaseController;
    }
  }
}
