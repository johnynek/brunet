/**
 * Dependencies
 * Brunet.IAHPacketHandler
 * Brunet.IPacketSender
 * Brunet.NumberSerializer
 * Brunet.AddressParser
 * Brunet.Address
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.Node
 * Brunet.Packet
 */

namespace Brunet
{

  /**
   * Implements the Packet Forwarding protocol which is
   * used to Bootstrap new connections on the network (and
   * potentially for other uses in the future).
   */
  public class PacketForwarder:IAHPacketHandler
  {

    /*private static readonly log4net.ILog _log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    protected Address _local;

    public PacketForwarder(Address local)
    {
      _local = local;
    }

    /**
     * This handles the packet forwarding protocol
     */
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
      if (p.Destination.IsUnicast) {
        Node n = (Node) node;
        AHPacket f_pack = UnwrapPacket(p);
        /*_log.Info("Forwarding source: " + f_pack.Source.ToString()
        		   + " forwarder: " + _local.ToString()
        		   + " destination: " + f_pack.Destination.ToString()
        		   + " P: " + p.ToString());*/
        if( f_pack.Source.Equals( _local ) ) {
          n.Send(f_pack, from);
        }
        else {
          //The sender made an incorrect packet:
          //_log.Error("Forwarder: Wrapped Source != Local");
        }
      }
      else {
        //_log.Error("Forward to NONUNICAST address: " + p.Destination.ToString());
      }
    }

    public bool HandlesAHProtocol(AHPacket.Protocol type)
    {
      return (type == AHPacket.Protocol.Forwarding);
    }

    /**
     * Make forward packet
     * @param packet_to_wrap the originally, fully constructed packet
     * @param forwarder the address to send the forward through
     * @param ttl_to_forwarder the ttl to use to reach the forwarder.
     *
     * The source of the resulting packet will be the source from
     * the packet_to_wrap.  The "next destination" will be the destination
     * from the packet_to_wrap, and the "next ttl" will be the the ttl
     * from the packet_to_wrap
     * 
     * This wraps a packet which was going A->B, to A->C->B where
     * C is the forwarder.
     */
    static public AHPacket WrapPacket(Address forwarder,
                                      short ttl_to_forwarder,
                                      AHPacket packet_to_wrap)
    {
      //System.Console.WriteLine("Packet to wrap: {0}", packet_to_wrap);
      byte[] whole_packet = new byte[packet_to_wrap.Length];
      packet_to_wrap.CopyTo(whole_packet, 0);
      //Change the source address to forwarder:
      int offset_to_src_add = 5;
      forwarder.CopyTo(whole_packet, offset_to_src_add);
      //Put the whole packet into the payload of a new packet:
      AHPacket result = new AHPacket(0, ttl_to_forwarder,
                                     packet_to_wrap.Source,
                                     forwarder,
                                     AHPacket.Protocol.Forwarding,
                                     whole_packet);
      //System.Console.WriteLine("Result: {0}", result);
      return result;
    }

    /**
     * @todo make NUnit test for this method
     * @param p the packet to forward
     * @return the unwrapped packet
     */
    static public AHPacket UnwrapPacket(AHPacket p)
    {
      //System.Console.WriteLine("Packet to Unwrap: {0}", p);
      AHPacket result = new AHPacket( p.PayloadStream, p.PayloadLength );
      //System.Console.WriteLine("Result: {0}", result);
      return result;
    }
  }

}
