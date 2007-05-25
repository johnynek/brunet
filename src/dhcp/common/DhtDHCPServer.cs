#define DHCP_DEBUG
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

namespace Ipop {
  public class DhtDHCPServer: DHCPServer {
    protected FDht _dht;

    public DhtDHCPServer(byte []server_ip, FDht dht) {
      _dht = dht;
      this.ServerIP = server_ip;
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
      Hashtable [] results = DhtOp.Get(ns_key, _dht);
      if(results == null) {
        Console.Error.WriteLine("Namespace does not exist");
        return null;
      }

      string xml_str = (string)results[0]["value_string"];
      if (xml_str == null) {
        Console.Error.WriteLine("Namespace does not exist");
        return null;
      }
      XmlSerializer serializer = new XmlSerializer(typeof(IPOPNamespace));
      TextReader stringReader = new StringReader(xml_str);
      IPOPNamespace ipop_ns = (IPOPNamespace) serializer.Deserialize(stringReader);
      DHCPLease dhcp_lease = new DhtDHCPLease(_dht, ipop_ns);
      leases[ipop_namespace] = dhcp_lease;
      return dhcp_lease;
    }

    protected override DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet) {
      DhtDHCPLeaseParam dht_param = new DhtDHCPLeaseParam(packet.yiaddr, packet.StoredPassword, DHCPCommon.StringToBytes(packet.NodeAddress, ':'));
      DHCPLeaseResponse ret = dhcp_lease.GetLease(dht_param);
      return ret;
    }
  }
}
