using System;
using System.Text;

namespace Ipop {
  public class IPRouterConfigUpdate {
    public static void Main(string []args) {
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);
      config.ipop_namespace = args[2];
      IPRouterConfigHandler.Write(args[1], config);
    }
  }
}