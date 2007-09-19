using System;
using System.Text;
using System.IO;

using System.Globalization;
using System.Collections;
using System.Runtime.Remoting.Lifetime;

using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;

using System.Diagnostics;

namespace Ipop {
  public class DhtDHCPServer: DHCPServer {
    protected Dht _dht;

    public DhtDHCPServer(byte []server_ip, Dht dht) {
      _dht = dht;
      this.ServerIP = new byte[4] {255, 255, 255, 255};
      this.leases = new SortedList();
      //do not have to even be concerned about brunet namespace so far
    }

    protected override bool IsValidBrunetNamespace(string brunet_namespace) {
      return true;
    }

    protected override DHCPLease GetDHCPLease(string ipop_namespace) {
      if (leases.ContainsKey(ipop_namespace)) {
        return (DHCPLease) leases[ipop_namespace];
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
      DHCPLease dhcp_lease = new DhtDHCPLease(_dht, ipop_ns);
      leases[ipop_namespace] = dhcp_lease;
      return dhcp_lease;
    }

    protected override DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet) {
      DhtDHCPLeaseParam dht_param = new DhtDHCPLeaseParam(packet.yiaddr, packet.NodeAddress);
      DHCPLeaseResponse ret = dhcp_lease.GetLease(dht_param);
      return ret;
    }
  }
}
