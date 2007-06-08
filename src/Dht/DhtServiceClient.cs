using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet.Dht;
using System;

namespace Ipop {
  public class DhtServiceClient {
    public static IDht GetSoapDhtClient() {
      IDht dht = (IDht)Activator.GetObject(typeof(IDht), "http://127.0.0.1:64221/sd.rem");
      return dht;
    }

    public static IDht GetXmlRpcDhtClient() {
      IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
      proxy.Url = "http://127.0.0.1:64221/xd.rem";
      return proxy;
    }

    public static IBlockingQueue GetXmlRpcBlockingQueue(string uri) {
      IXmlRpcBlockqingQueue proxy = (IXmlRpcBlockqingQueue)XmlRpcProxyGen.Create(typeof(IXmlRpcBlockqingQueue));
      proxy.Url = "http://127.0.0.1:64221/" + uri;      
      return proxy;
    }

    public static IBlockingQueue GetSoapBlockingQueue(string uri) {
      IBlockingQueue bq = (IBlockingQueue)Activator.GetObject(typeof(IBlockingQueue), "http://127.0.0.1:64221/" + uri);
      return bq;
    }
  }

  /// <summary>
  /// The XmlRpc interface for the Dht which is used on the client side
  /// </summary>
  public interface IXmlRpcDht : IDht, CookComputing.XmlRpc.IXmlRpcProxy {
    [XmlRpcMethod]
    new DhtGetResult[] Get(string key);
    [XmlRpcMethod]
    new string Create(string key, string value, string password, int ttl);
    [XmlRpcMethod]
    new string Put(string key, string value, string password, int ttl);
    [XmlRpcMethod]
    new string GetAsBlockingQueue(string key);
  }

  public interface IXmlRpcBlockqingQueue : IBlockingQueue, CookComputing.XmlRpc.IXmlRpcProxy {
    [XmlRpcMethod]
    new object Dequeue();
    [XmlRpcMethod]
    new object Dequeue(int millisec, out bool timedout);
    [XmlRpcMethod]
    new void Close();
  }
}
