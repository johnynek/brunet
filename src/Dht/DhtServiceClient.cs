using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet.Dht;
using System;

namespace Ipop {
  public class DhtServiceClient     {
    public static IDht GetSoapDhtClient() {
      IDht dht = (IDht)Activator.GetObject(typeof(IDht), "http://127.0.0.1:64221/sd.rem");
      return dht;
    }

    public static IDht GetXmlDhtClient() {
      IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
      proxy.Url = "http://127.0.0.1:64221/xd.rem";
      return proxy;
    }
  }

  /// <summary>
  /// The XmlRpc interface for the Dht which is used on the client side
  /// </summary>
  public interface IXmlRpcDht : IDht, CookComputing.XmlRpc.IXmlRpcProxy
  {
    [XmlRpcMethod]
    new DhtGetResult[] Get(string key);
    [XmlRpcMethod]
    new string Create(string key, string value, string password, int ttl);
    [XmlRpcMethod]
    new string Put(string key, string value, string password, int ttl);
  }
}
