using System;
using System.Runtime.Remoting;
using System.Text;
using System.Collections;
using Brunet;
using CookComputing.XmlRpc;
using Brunet.Dht;

namespace Ipop
{
  /// <summary>
  /// Dht stub using XmlRpc protocol
  /// </summary>
  public class XmlRpcDht : MarshalByRefObject,IDht {
    [NonSerialized]
    private FDht _dht;
    [NonSerialized]
    private DhtOp _dhtOp;

    public XmlRpcDht(FDht dht) {
      this._dht = dht;
      this._dhtOp = new DhtOp(_dht);
    }

    [XmlRpcMethod]
    public DhtGetResult[] Get(string key) {
      return _dhtOp.Get(key);
    }

    [XmlRpcMethod]
    public string Create(string key, string value, string password, int ttl) {
      return _dhtOp.Create(key, value, password, ttl);
    }

    [XmlRpcMethod]
    public string Put(string key, string value, string password, int ttl) {
      return _dhtOp.Create(key, value, password, ttl);
    }

    [XmlRpcMethod]
    public string GetAsBlockingQueue(string key) {
      BlockingQueue bq = _dhtOp.GetAsBlockingQueue(key);
      XmlRpcBlockingQueueAdapter adpt = new XmlRpcBlockingQueueAdapter(bq);
      return adpt.Uri;
    }
  }
}
