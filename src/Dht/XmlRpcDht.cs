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
    private FDht dht;

    public XmlRpcDht(FDht dht) {
      this.dht = dht;
    }

    [XmlRpcMethod] 
    public DhtGetResult[] Get(string key) {
      return DhtOp.Get(key, this.dht);
    }

    [XmlRpcMethod]
    public string Create(string key, string value, string password, int ttl) {
      return DhtOp.Create(key, value, password, ttl, this.dht);
    }

    [XmlRpcMethod]
    public string Put(string key, string value, string password, int ttl) {
      return DhtOp.Create(key, value, password, ttl, this.dht);
    }
  }
}
