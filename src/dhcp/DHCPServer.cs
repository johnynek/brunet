using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Lifetime;

namespace Ipop {
  class Server {
    static void Main(string[] args) {
      ChannelServices.RegisterChannel(new TcpChannel(Int32.Parse(args[0])));
      byte []serverIP = new byte[4] {192, 168, 0 , 1};
      SoapDHCPServer server = new SoapDHCPServer(serverIP, args[1]);
      RemotingServices.Marshal(server, "DHCPServer.rem");

      ISponsor sponsor = new LifeTimeSponsor();
      // get lifetime manager for the remote object
      ILease lifetime = (ILease)server.GetLifetimeService();
      // registering our sponsor
      lifetime.Register(sponsor);
      while(true) System.Threading.Thread.Sleep(9999999);
    }
  }
}
