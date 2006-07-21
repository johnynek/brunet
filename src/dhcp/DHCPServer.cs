using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Lifetime;

namespace Ipop {
  class Server {
    static void Main(string[] args) {
      ChannelServices.RegisterChannel(new TcpChannel(61234));
      byte []serverIP = new byte[4] {192, 168, 0 , 1};
      DHCPServer server = new DHCPServer(serverIP, args[0]);
      RemotingServices.Marshal(server, "DHCPServer.rem");

      ISponsor sponsor = new LifeTimeSponsor();
      // get lifetime manager for the remote object
      ILease lifetime = (ILease)server.GetLifetimeService();
      // registering our sponsor
      lifetime.Register(sponsor);

      Console.WriteLine("Press enter to shutdown"); 
      Console.ReadLine(); 
    }
  }
}