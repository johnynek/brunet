using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Lifetime;
using CookComputing.XmlRpc;
using System.Threading;
using Brunet;
using Brunet.Dht;

namespace Ipop {
  /// <summary>
  /// A Dht Server thread listens to Soap and XmlRpc requests
  /// Soap URL: http://localhost:64221/sd.rem
  /// XmlRpc URL: http://localhost:64221/xd.rem
  /// </summary>
  public class DhtServer {
    private DhtAdapter _sd, _xd;
    public  DhtServer(int port) {
      if(port == 0) {
        throw new Exception("Must be started with a valid specific port number!");
      }
      IServerChannelSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
      chain.Next = new SoapServerFormatterSinkProvider();

      IDictionary props = new Hashtable();
      props.Add("port", port);
      props.Add("name", "dhtsvc");
      HttpChannel channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(channel, false);
    }

    public void Stop(){
      RemotingServices.Disconnect(_sd);
      RemotingServices.Disconnect(_xd);
    }

    public void Update(Dht dht) {
      _sd = new SoapDht(dht);
      RemotingServices.Marshal(_sd, "sd.rem");

      _xd = new XmlRpcDht(dht);
      RemotingServices.Marshal(_xd, "xd.rem");
    }
  }
}