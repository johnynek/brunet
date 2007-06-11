using System;
using System.Text;
using System.Collections;
using Brunet.Dht;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;

namespace Ipop {
  public class SoapDht : MarshalByRefObject, IDht {
    [NonSerialized]
    private FDht _dht;
    private DhtOp _dhtOp;

    public SoapDht(FDht dht) {
      this._dht = dht;
      this._dhtOp = new DhtOp(_dht);
    }

    public SoapDht() {;}

    public string Create(string key, string value, string password, int ttl) {
      return _dhtOp.Create(key, value, password, ttl);
    }

    public string Put(string key, string value, string password, int ttl) {
      return _dhtOp.Put(key, value, password, ttl);
    }

    public DhtGetResult[] Get(string key) {
      return _dhtOp.Get(key);
    }
  }
}
