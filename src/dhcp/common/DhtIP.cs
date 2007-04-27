#define DHCP_DEBUG

using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class DhtIP {
    public static void ReleaseIP(FDht _dht, string ipop_namespace, string ip, string password) {
      if (password == null) {
        Console.Error.WriteLine("No password provided for ReleaseIP.");
        return;
      }
      DhtOp.Delete("dhcp:ip:" + ip, password, _dht);
    }

    public static bool GetIP(FDht _dht, string ipop_namespace, string ip,
                            int leasetime, byte [] brunet_id, ref string password) {
      string key = "dhcp:ipop_namespace:" + ipop_namespace + ":ip:" + ip;
      string output = DhtOp.Create(key, brunet_id, password, leasetime, _dht);
      if(output == null) {
        return false;
      }
      password = output;
      return true;
    }
  }
}
