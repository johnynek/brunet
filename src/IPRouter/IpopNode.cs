using System;
using System.Net;
using Brunet;
using Brunet.Dht;
using System.Collections;
using System.Threading;

namespace Ipop {
  public class IpopNode {
    public readonly string IpopNamespace, BrunetNamespace;
    public readonly Address Address;
    public readonly Ethernet Ether;

    //Services
    public readonly DHCPServer DHCPServer;
    public readonly Dht Dht;
    public readonly IPHandler IPHandler;
    public readonly Routes Routes;
    public readonly StructuredNode Brunet;
    public readonly IpopInformation IpopInfo;
    public readonly DhtDNS DhtDNS;

    private string _ip;
    // Need a mechanism to update the _ipop_info.ip
    public string IP {
      get { return _ip; }
      set {
        _ip = value;
        IpopInfo.ip = _ip.ToString();
      }
    }

    public string Netmask;
    public byte [] MAC;

    public IpopNode(string ipop_namespace, string brunet_namespace,
                       Address address, Ethernet ether) {
      IpopNamespace = ipop_namespace;
      BrunetNamespace = brunet_namespace;
      Address = address;
      Ether = ether;

      Brunet = Brunet_Common.CreateStructuredNode(IPRouter.config);
      IpopInfo = new IpopInformation(Brunet, "IPRouter",
                                       ipop_namespace, brunet_namespace);
      Dht = Brunet_Common.RegisterDht(Brunet);
      Brunet_Common.StartServices(Brunet, Dht, IPRouter.config);
      (new Thread(Brunet.Connect)).Start();
      IPHandler = new IPHandler(this);
      Routes = new Routes(Dht, ipop_namespace);
      DHCPServer = new DhtDHCPServer(Dht);
      DhtDNS = new DhtDNS(this);
    }
  }
}
