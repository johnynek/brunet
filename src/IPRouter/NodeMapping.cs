using System;
using System.Net;
using Brunet;
using Brunet.Dht;
using System.Collections;
using System.Threading;

namespace Ipop {
  public class NodeMapping {
    private static readonly int sleep_min = 60, sleep_max = 3600;
    private int sleep = 60;
    private DateTime runtime;

    // These should never change
    public readonly string ipop_namespace, brunet_namespace;
    public readonly Address address;
    public readonly Ethernet ether;

    public string netmask;
    public byte [] mac;

    //Services
    public DHCPClient dhcpClient;
    public Dht dht;
    private IpopInformation _ipop_info;
    public IPHandler iphandler;
    public Routes routes;

    private IPAddress _ip;
    // Need a mechanism to update the _ipop_info.ip
    public IPAddress ip {
      get { return _ip; }
      set {
        _ip = value;
        _ipop_info.ip = _ip.ToString();
      }
    }

    private StructuredNode _brunet;
    // If we change Nodes we have to re-initialize
    public StructuredNode brunet {
      get { return _brunet; }
      set {
        _brunet = value;
        if(value != null) {
          _ipop_info = new IpopInformation(_brunet, "IPRouter",
                                ipop_namespace, brunet_namespace);
        }
      }
    }

    public NodeMapping(string ipop_namespace, string brunet_namespace,
                       Address address, Ethernet ether) {
      this.ipop_namespace = ipop_namespace;
      this.brunet_namespace = brunet_namespace;
      this.address = address;
      this.ether = ether;
    }

    public void BrunetStart()
    {
      IPRouterConfig config = IPRouter.config;
      brunet = Brunet_Common.CreateStructuredNode(config);
      dht = Brunet_Common.RegisterDht(brunet);
      Brunet_Common.StartServices(brunet, dht, config);
//  We do not support the Disconnect method on overload in Brunet as of now 
//  as it isn't tweaked for IPRouter.
//      brunet.DepartureEvent += DisconnectHandler;
//      brunet.DisconnectOnOverload = true;

      (new Thread(brunet.Connect)).Start();
      iphandler = new IPHandler(this);
      routes = new Routes(dht, ipop_namespace);

      dhcpClient = new DhtDHCPClient(dht);
    }

    private void DisconnectHandler(object o, EventArgs ea)
    {
      (new Thread(new ThreadStart(SleepAndRestart))).Start();
    }

    private void SleepAndRestart()
    {
      Brunet_Common.RemoveHandlers();
      brunet = null;
      dht = null;
      iphandler = null;
      routes = null;

      DateTime now = DateTime.UtcNow;
      Thread.Sleep(sleep * 1000);
      if(now - runtime < TimeSpan.FromSeconds(sleep_max)) {
        sleep *= 2;
        sleep = (sleep > sleep_max) ? sleep_max : sleep;
      }
      else {
        sleep /= 2;
        sleep = (sleep < sleep_min) ? sleep_min : sleep;
      }
      BrunetStart();
    }
  }

//  We need a thread to remove a node mapping if our use of it expires -- that is it isn't used for a certain period of time
  public class NodeMappings {
    private ArrayList nodeMappings;

    public NodeMappings() {
      nodeMappings = new ArrayList();
    }

    public void AddNodeMapping(NodeMapping node) {
      nodeMappings.Add(node);
    }

    public NodeMapping GetNode(IPAddress ip) {
      foreach (NodeMapping node in nodeMappings) {
        if(node.ip.Equals(ip))
          return node;
      }
      return null;
    }

    public bool RemoveNodeMapping(IPAddress ip) {
      for (int i = 0; i < nodeMappings.Count; i++) {
        if(((NodeMapping) nodeMappings[i]).ip.Equals(ip)) {
          nodeMappings.Remove(i);
          return true;
        }
      }
      return false;
    }
  }
}
