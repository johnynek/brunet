using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class DhtIP {
    public static bool GetIP(Dht _dht, string ipop_namespace, string ip,
                            int leasetime, byte [] brunet_id) {
      string key = "dhcp:ipop_namespace:" + ipop_namespace + ":ip:" + ip;
      return _dht.Create(key, brunet_id, leasetime);
    }
  }
}
