using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet.Dht;
using System;

namespace Ipop {
  public class DhtServiceClient {
    public static ISoapDht GetSoapDhtClient() {
      ISoapDht dht = (ISoapDht)Activator.GetObject(typeof(ISoapDht), "http://127.0.0.1:64221/sd.rem");
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
    new bool Create(string key, string value, int ttl);
    [XmlRpcMethod]
    new bool Put(string key, string value, int ttl);
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
    IAsyncResult BeginCreateWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndCreate(IAsyncResult iasr);
    [XmlRpcBegin("Put")]
    IAsyncResult BeginPutWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndPut(IAsyncResult iasr);
  }

  public interface ISoapDht : IDht {
    IBlockingQueue GetAsBlockingQueue(string key);
  }

  
  /**
   * Dht client side operations
   */
  public class DhtClientOp {
    public delegate bool PutOp(string key, string value,int ttl);
    public delegate DhtGetResult[] GetOp(string key);
    
    private IDht _dht;

    public DhtClientOp(IDht dht) {
      this._dht = dht;
    }

    public IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state) {
      GetOp op = new GetOp(this._dht.Get);
      IAsyncResult ar = op.BeginInvoke(key, acb, state);
      return ar;
    }

    public IAsyncResult BeginPutWithCallback(string key, string value, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Put);
      IAsyncResult ar = op.BeginInvoke(key, value, ttl, acb, state);
      return ar;
    }

    public IAsyncResult BeginCreateWithCallback(string key, string value, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Create);
      IAsyncResult ar = op.BeginInvoke(key, value, ttl, acb, state);
      return ar;
    }
  }

  /**
   * Asynchronous BlockingQueue operations
   */
  public class BlockingQueueOp {
    public delegate object DequeueOp(int millisec, out bool timedout);

    private IBlockingQueue _bq;

    public BlockingQueueOp(IBlockingQueue bq) {
      this._bq = bq;
    }

    public IAsyncResult BeginDequeueOp(int millisec, out bool timedout, AsyncCallback acb, object state) {
      DequeueOp op = new DequeueOp(this._bq.Dequeue);
      IAsyncResult ar = op.BeginInvoke(millisec, out timedout, acb, state);
      return ar;
    }
  }
}
