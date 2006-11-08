using System;
using System.Text;

namespace Ipop {
  public class IPRouterConfigUpdate {
    public static void Main(string []args) {
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);
      config.DevicesToBind = new string[] {"eth0"};
      IPRouterConfigHandler.Write(args[0], config);
    }
  }
}