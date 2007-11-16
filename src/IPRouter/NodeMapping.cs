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

    public DHCPClient dhcpClient;
    public Dht dht;
    public Ethernet ether;
    public IPAddress ip;
    public IPHandler iphandler;
    public Routes routes;
    public string netmask;
    public string ipop_namespace;
    public Address address;
    public byte [] mac;
    public StructuredNode brunet {
      get { return _brunet; }
      set {
        _brunet = value;
        if(value != null) {
          new IpopInformation(_brunet, "IPRouter");
        }
      }
    }
    private StructuredNode _brunet;

    public void BrunetStart()
    {
      IPRouterConfig config = IPRouter.config;
      brunet = Brunet_Common.CreateStructuredNode(config);
      dht = Brunet_Common.RegisterDht(brunet);
      Brunet_Common.StartServices(node, dht, config);
      brunet.DepartureEvent += DisconnectHandler;
      brunet.disconnect_on_overload = true;

      brunet.Connect();
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
      Brunet_Common.DisconnectNode(brunet, true);
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
