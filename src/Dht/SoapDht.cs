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
    private FDht dht;

    public SoapDht(FDht dht) {
      this.dht = dht;
    }

    public SoapDht() {;}

    public string Create(string key, string value, string password, int ttl) {
      return DhtOp.Create(key, value, password, ttl, this.dht);
    }

    public string Put(string key, string value, string password, int ttl) {
      return DhtOp.Put(key, value, password, ttl, this.dht);
    }

    public DhtGetResult[] Get(string key) {
      return DhtOp.Get(key, this.dht);
    }
  }
}
