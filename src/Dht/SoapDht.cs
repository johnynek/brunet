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
    private Dht _dht;

    public SoapDht(Dht dht) {
      this._dht = dht;
    }

    public SoapDht() {;}

    public bool Create(string key, string value, int ttl) {
      return _dht.Create(key, value, ttl);
    }

    public bool Put(string key, string value, int ttl) {
      return _dht.Put(key, value, ttl);
    }

    public DhtGetResult[] Get(string key) {
      return _dht.Get(key);
    }
  }
}
