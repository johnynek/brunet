/**
 * Dependencies : 
 * Brunet.AHPacket
 * Brunet.Edge
 */

namespace Brunet
{

  /**
   * When objects want to handle packets delivered to local nodes
   * they implement this interface and Subscribe to particular
   * protocols on the Node
   */
  public interface IAHPacketHandler
  {

  /**
   * @param node The node that got the packet
   * @param p the packet
   * @param from the edge we got the packet from
   */
    void HandleAHPacket(object node, AHPacket p, Edge from);

  /**
   * @return true if you handle this type
   */
    bool HandlesAHProtocol(AHPacket.Protocol type);
  }

}
