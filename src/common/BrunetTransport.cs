/** The class implements the unreliable transport
    provided by Brunet; assuming that the node is connected to the network
 **/
using Brunet;
namespace Ipop {
  public class BrunetTransport {
    Node brunetNode;
    public BrunetTransport(Node node) {
      brunetNode = node;
    }
    //method to send a packet out on the network
    public void SendPacket(AHAddress target, byte[] packet) {
      AHPacket p = new AHPacket(0, 30,   brunetNode.Address,
        target, AHPacket.AHOptions.Exact,
        AHPacket.Protocol.IP, packet);
      brunetNode.Send(p);
    }
  }
}
