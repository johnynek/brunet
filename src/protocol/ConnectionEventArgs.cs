/*
 * Brunet.Address;
 * Brunet.Edge
 * Brunet.ConnectionType;
 */

namespace Brunet
{

  /**
   * When a Connection is created, an EventHandler is called
   * with (edge, ConnectionEventArgs) as the parameters
   */

  public class ConnectionEventArgs:System.EventArgs
  {

    public Address RemoteAddress;
    public ConnectionType ConnectionType;
    public Edge Edge;
    public int Index;

    public ConnectionEventArgs(Address remote,
                               Edge edge,
                               ConnectionType t,
                               int index)
    {
      this.RemoteAddress = remote;
      this.ConnectionType = t;
      this.Edge = edge;
      this.Index = index;
    }
  }

}
