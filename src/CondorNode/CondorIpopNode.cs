using Brunet.Applications;
using Ipop.DhtNode;
using System;

/**
\namespace Ipop::CondorNode Defines CondorIpopNode which uses DhtNode with a
static DNS server.
*/
namespace Ipop.CondorNode {
  /**
  <summary>This class provides an IpopNode which extends DhtIpopNode and
  supplements DhtDNS with a staticly mapped DNS</summary>
  */
  public class CondorIpopNode: DhtIpopNode {
    /**
    <summary></summary>
    <param name=""></param>
    <param name=""></param>
    */
    public CondorIpopNode(String NodeConfigPath, String IpopConfigPath):
      base(NodeConfigPath, IpopConfigPath) {
      _dns = new CondorDNS();
    }

    /**
    <summary></summary>
    <param name=""></param>
    <param name=""></param>
    */
    public override void UpdateAddressData(string IP, string Netmask) {
      base.UpdateAddressData(IP, Netmask);
      ((CondorDNS) _dns).UpdatePoolRange(IP, Netmask);
    }

    /**
    <summary></summary>
    <param name=""></param>
    <param name=""></param>
    */
    public static new void Main(String[] args) {
      CondorIpopNode node = new CondorIpopNode(args[0], args[1]);
      node.Run();
    }
  }
}
