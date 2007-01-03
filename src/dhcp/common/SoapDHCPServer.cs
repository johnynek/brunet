using System;
using System.Text;
using System.IO;

using System.Globalization;
using System.Collections;
using System.Runtime.Remoting.Lifetime;

using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  public class SoapDHCPServer : DHCPServer {
    string brunet_namespace;
    DHCPServerConfig config;

    public SoapDHCPServer() {;} // Dummy for Client

    public SoapDHCPServer(byte [] server_ip, string filename) { 
      ServerIP = server_ip;
      config = DHCPServerConfigurationReader.ReadConfig(filename);
      brunet_namespace = config.brunet_namespace;
      leases = new SortedList();

      foreach(IPOPNamespace item in this.config.ipop_namespace) {
        DHCPLease lease = new SoapDHCPLease(item);
        leases.Add(item.value, lease);
      }
    }

    protected override DHCPLease GetDHCPLease(string ipop_namespace) {
      if (!leases.ContainsKey(ipop_namespace))
        return null;
      return (DHCPLease) leases[ipop_namespace];
    }

    protected override bool IsValidBrunetNamespace(string brunet_namespace) {
      return (this.brunet_namespace.Equals(brunet_namespace));
    }

    protected override DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet) {
      return dhcp_lease.GetLease(new SoapDHCPLeaseParam(DHCPCommon.StringToBytes(packet.NodeAddress, ':')));
    }

    public override Object InitializeLifetimeService() {
      ILease lease = (ILease)base.InitializeLifetimeService();
      if (lease.CurrentState == LeaseState.Initial)
        lease.InitialLeaseTime = TimeSpan.FromDays(365);
      return lease;
    }
  }
}