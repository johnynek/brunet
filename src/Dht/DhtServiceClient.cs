using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet.Dht;
using System;
using System.Collections;

namespace Ipop {
  public partial class DhtServiceClient {
    public static IXmlRpcDht GetXmlRpcDhtClient() {
      return GetXmlRpcDhtClient(64221);
    }

    public static IXmlRpcDht GetXmlRpcDhtClient(int port) {
      IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
      proxy.Url = "http://127.0.0.1:" + port + "/xd.rem";
      return proxy;
    }
  }

  /// <summary>
  /// The XmlRpc interface for the Dht which is used on the client side
  /// </summary>
  public interface IXmlRpcDht : IDht, CookComputing.XmlRpc.IXmlRpcProxy {
    [XmlRpcMethod]
    new DhtGetResult[] Get(string key);
    [XmlRpcMethod]
    new bool Create(string key, string value, int ttl);
    [XmlRpcMethod]
    new bool Put(string key, string value, int ttl);
    [XmlRpcMethod]
    new string BeginGet(string key);
    [XmlRpcMethod]
    new DhtGetResult ContinueGet(string token);
    [XmlRpcMethod]
    new void EndGet(string token);

    [XmlRpcMethod]
    new IDictionary GetDhtInfo();

    [XmlRpcBegin("Get")]
    IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state);
    [XmlRpcEnd]
    DhtGetResult[] EndGet(IAsyncResult iasr);
    [XmlRpcBegin("Create")]
    IAsyncResult BeginCreateWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndCreate(IAsyncResult iasr);
    [XmlRpcBegin("Put")]
    IAsyncResult BeginPutWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndPut(IAsyncResult iasr);
  }
}
