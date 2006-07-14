using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace Ipop {
  class Server {
    static void Main(string[] args) {
      ChannelServices.RegisterChannel(new TcpChannel(61234));
      byte []serverIP = new byte[4] {192, 168, 0 , 1};
      DHCPServer server = new DHCPServer(serverIP);
      RemotingServices.Marshal(server, "DHCPServer.rem");
      Console.WriteLine("Press enter to shutdown"); 
      Console.ReadLine(); 
    }
  }
}