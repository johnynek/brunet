/** The class implements the unreliable transport
    provided by Brunet; assuming that the node is connected to the network
 **/
using Brunet;
using Brunet.Dht;
namespace Ipop {
  public class BrunetTransport {
    protected Node _brunet_node;
    public Node Node {
      get {
	return _brunet_node;
      }
    }
    protected FDht _dht;
    public FDht Dht {
      get {
	return _dht;
      }
    }

    protected IPPacketHandler _ip_handler;

    public BrunetTransport(Node node) {
      _dht = null;
      _brunet_node = node;
    }
    public BrunetTransport(Node node, FDht dht) {
      _brunet_node = node;
      _dht = dht;
    }
    //method to send a packet out on the network
    public void SendPacket(AHAddress target, byte[] packet) {
      AHPacket p = new AHPacket(0, 30,   _brunet_node.Address,
        target, AHPacket.AHOptions.Exact,
        AHPacket.Protocol.IP, packet);
      _brunet_node.Send(p);
    }
    public void Resubscribe(IPPacketHandler ip_handler) {
      _brunet_node.Unsubscribe(AHPacket.Protocol.IP, _ip_handler);
      _ip_handler = ip_handler;
      _brunet_node.Subscribe(AHPacket.Protocol.IP, ip_handler);
    }
  }
}
