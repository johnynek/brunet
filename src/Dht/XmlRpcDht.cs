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
    private Dht _dht;

    public XmlRpcDht(Dht dht) {
      this._dht = dht;
    }

    [XmlRpcMethod]
    public DhtGetResult[] Get(string key) {
      return _dht.Get(key);
    }

    [XmlRpcMethod]
    public bool Create(string key, string value, int ttl) {
      return _dht.Create(key, value, ttl);
    }

    [XmlRpcMethod]
    public bool Put(string key, string value, int ttl) {
      return _dht.Put(key, value, ttl);
    }
  }
}
