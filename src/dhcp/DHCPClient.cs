using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;

namespace Ipop {
  class DHCPClient {
    public static void DHCPInit(string ipAddress) {
      TcpChannel ch = new TcpChannel();
      ChannelServices.RegisterChannel(ch);
      RemotingConfiguration.RegisterWellKnownClientType(
        typeof(DHCPServer),
        "tcp://" + ipAddress + "/DHCPServer.rem");
    }
    public static DecodedDHCPPacket SendMessage(
      DecodedDHCPPacket packet) {
      DHCPServer server = new DHCPServer();
      DecodedDHCPPacket response = server.SendMessage(packet);
      return response;
    }
  }
}
