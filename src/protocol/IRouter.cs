/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 */

namespace Brunet
{

  /**
   * Different Address classes are routed differently.
   * There are different Router classes for each different
   * routing rule.  They each implement this interface
   */

  public interface IRouter
  {

    /**
     * @return the Type of the Address this router supports 
     */
    System.Type RoutedAddressType
    {
      get;
      }

      ConnectionTable ConnectionTable
      {
        set;
        }

        /**
         * @param p the AHPacket to route
         * @param from The edge the packet came from
         * @param deliverlocally set to true if the local node should Announce it
         * @return the number of edges the packet it Sent on.
         */
        int Route(Edge from, AHPacket p, out bool deliverlocally);
  }

}
