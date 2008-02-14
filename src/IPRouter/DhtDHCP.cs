using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class DhtDHCPLeaseController: DHCPLeaseController {
    protected Dht _dht;

    public DhtDHCPLeaseController(Dht dht, IPOPNamespace config):base(config) {
      _dht = dht;
    }

    public override DHCPReply GetLease(byte[] RequestedAddr, bool Renew, DecodedDHCPPacket packet) {
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
          string str_addr = IPOP_Common.BytesToString(RequestedAddr, '.');
          string key = "dhcp:ipop_namespace:" + namespace_value + ":ip:" + str_addr;
          try {
            res = _dht.Create(key, (string) packet.NodeAddress, leasetime);
          }
          catch {
            res = false;
          }
          if(res) {
            if(packet.hostname != null) {
              _dht.Put(packet.hostname + DhtDNS.DNS_SUFFIX, str_addr, leasetime);
            }
            _dht.Put("multicast.ipop_vpn", (string) packet.NodeAddress, leasetime);
            _dht.Put((string) packet.NodeAddress, key + "|" + DateTime.Now.Ticks, leasetime);
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

    public DhtDHCPServer(byte []server_ip, Dht dht) {
      _dht = dht;
      this.ServerIP = new byte[4] {10, 250, 0, 1};
      this._dhcp_lease_controllers = new SortedList();
      //do not have to even be concerned about brunet namespace so far
    }

    protected override bool IsValidBrunetNamespace(string brunet_namespace) {
      return true;
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
      DHCPLeaseController dhcpLeaseController = new DhtDHCPLeaseController(_dht, ipop_ns);
      _dhcp_lease_controllers[ipop_namespace] = dhcpLeaseController;
      return dhcpLeaseController;
    }
  }
}
