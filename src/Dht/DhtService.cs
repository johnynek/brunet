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
    public static Thread StartDhtServerAsThread(Dht dht, int port) {
      Thread DhtThread = new Thread(DhtServer.StartDhtServer);
      Hashtable ht = new Hashtable();
      ht["dht"] = dht;
      ht["port"] = port;
      DhtThread.Start(ht);
      return DhtThread;
    }

    public static void StartDhtServer(object data) {
      Hashtable ht = (Hashtable) data;
      StartDhtServer((Dht) ht["dht"], (int) ht["port"]);
    }

    public static void StartDhtServer(Dht dht, int port) {
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

      SoapDht sd = new SoapDht(dht);
      RemotingServices.Marshal(sd, "sd.rem");

      XmlRpcDht xd = new XmlRpcDht(dht);
      RemotingServices.Marshal(xd, "xd.rem");

      while (true) System.Threading.Thread.Sleep(Timeout.Infinite);
    }
  }
}