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

    public static IXmlRpcDht GetXmlRpcDhtClient() {
      IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
      proxy.Url = "http://127.0.0.1:64221/xd.rem";
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
    new string Create(string key, string value, string password, int ttl);
    [XmlRpcMethod]
    new string Put(string key, string value, string password, int ttl);
    [XmlRpcMethod]
    new string BeginGet(string key);
    [XmlRpcMethod]
    new DhtGetResult[] ContinueGet(string token);
    [XmlRpcMethod]
    new void EndGet(string token);
    
    
    [XmlRpcBegin("Get")]
    IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state);
    [XmlRpcEnd]
    DhtGetResult[] EndGet(IAsyncResult iasr);
    [XmlRpcBegin("Create")]
    IAsyncResult BeginCreateWithCallback(string key, string value, string password, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndCreate(IAsyncResult iasr);
    [XmlRpcBegin("Put")]
    IAsyncResult BeginPutWithCallback(string key, string value, string password, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndPut(IAsyncResult iasr);
  }

  public class AsyncDhtClient {
    public delegate string PutOp(string key, string value, string password, int ttl);
    public delegate DhtGetResult[] GetOp(string key);
    
    private IDht _dht;

    public AsyncDhtClient(IDht dht) {
      this._dht = dht;
    }

    public IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state) {
      GetOp op = new GetOp(this._dht.Get);
      IAsyncResult ar = op.BeginInvoke(key, acb, state);
      return ar;
    }

    public IAsyncResult BeginPutWithCallback(string key, string value, string password, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Put);
      IAsyncResult ar = op.BeginInvoke(key, value, password, ttl, acb, state);
      return ar;
    }

    public IAsyncResult BeginCreateWithCallback(string key, string value, string password, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Create);
      IAsyncResult ar = op.BeginInvoke(key, value, password, ttl, acb, state);
      return ar;
    }

  }
}
