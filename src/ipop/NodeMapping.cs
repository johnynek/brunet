using System.Net;
using Brunet;

namespace Ipop {
  public class NodeMapping {
    public IPAddress ip;
    public string netmask, nodeAddress, password, ipop_namespace;
    public byte [] mac;
    public BrunetTransport brunet;

    public NodeMapping() {
      ip = null;
      netmask = null;
      mac = null;
      brunet = null;
      nodeAddress = null;
      password = null;
      ipop_namespace = null;
    }
  }
}