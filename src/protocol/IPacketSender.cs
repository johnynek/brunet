/**
 * Brunet.Packet
 */

namespace Brunet
{

  /**
   * Node and Edge implement this interface
   */
  public interface IPacketSender
  {

  /**
   * Represents objects which can send packets
   */
    void Send(Packet p);
  }

}
