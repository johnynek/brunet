using System;

namespace Ipop {
  public class DhtNodeRun: BasicNode {
    protected String _ipop_config_path;

    public static void Main(String[] args) {
      DhtNodeRun node = new DhtNodeRun(args[0], args[1]);
      node.Run();
    }

    public DhtNodeRun(String NodeConfigPath, String IpopConfigPath): 
      base(NodeConfigPath) {
      _ipop_config_path = IpopConfigPath;
    }

    public override void Run() {
      CreateNode();
      new DhtIpopNode(_node, _dht, _ipop_config_path);
      _node.Connect();
      StopServices();
    }
  }
}