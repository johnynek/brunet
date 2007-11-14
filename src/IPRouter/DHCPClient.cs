using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;

using Brunet.Dht;

namespace Ipop {
  abstract public class DHCPClient {
    public DHCPServer _dhcp_server;
    abstract public DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet);
  }

  public class DhtDHCPClient: DHCPClient {
    public DhtDHCPClient(Dht dht) {
      _dhcp_server = new DhtDHCPServer(new byte[4] {192, 168, 0 , 1}, dht);
    }

    public override DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet) {
      return _dhcp_server.SendMessage(packet);
    }
  }
}