//using Brunet.Packet;
using Brunet;

namespace Brunet
{

  /**
   * The .Net event model suggests using System.EventHandler
   * for all events, but subclassing the System.EventArgs.
   * This class represents the arguments for the Event
   * which occurs when a packet arrives.
   */

  public class ReceivedPacketArgs:System.EventArgs
  {

    public Brunet.Packet packet;

    public ReceivedPacketArgs(Brunet.Packet p)
    {
      this.packet = p;
    }
  }

}
