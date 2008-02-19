using System;

namespace Ipop {
  public class NodeRun {
    public static void Main(String[] args) {
      BasicNode node = new BasicNode(args[0]);
      node.Run();
    }
  }
}