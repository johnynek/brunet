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
#if DHCP_DEBUG
      Console.WriteLine("searchig for key: {0}", ns_key);
#endif
      byte[] utf8_key = Encoding.UTF8.GetBytes(ns_key);
      //get a maximum of 1000 bytes only
      BlockingQueue[] q = _dht.GetF(utf8_key, 1000, null);
      //we do expect to get atleast 1 result
      ArrayList result = null;
      try{
        while (true) {
          RpcResult res = q[0].Dequeue() as RpcResult;
          result = res.Result as ArrayList;
          if (result == null || result.Count < 3) {
            continue;
          }
          break;
        }
      } catch (Exception) {
        return null;
      }
      ArrayList values = (ArrayList) result[0];
#if DHCP_DEBUG
      Console.WriteLine("# of matching entries: " + values.Count);
#endif
      string xml_str = null;
      foreach (Hashtable ht in values) {
#if DHCP_DEBUG
        Console.WriteLine(ht["age"]);
#endif
        byte[] data = (byte[]) ht["data"];
        xml_str = Encoding.UTF8.GetString(data);
#if DHCP_DEBUG
        Console.WriteLine(xml_str);
#endif
        break;
      }
      if (xml_str == null) {
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