/**
 * Depedencies
 * Brunet.Address
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 * Brunet.IRouter
 * Brunet.RwtaAddress
 * Brunet.AHPacket
 * Brunet.Edge
 */

using System;
using System.Collections;

namespace Brunet
{

  /**
   * Routes RwtaAddresses on the unstructured system
   */
  public class RwtaRouter:IRouter
  {

    protected ConnectionTable _connection_table;
    protected static Random _rand =  new Random(DateTime.Now.Millisecond);

    public RwtaRouter() 
    {
      
    }

    public System.Type RoutedAddressType
    {
      get
      {
        return typeof(RwtaAddress);
      }
    }

    virtual public ConnectionTable ConnectionTable
    {
      set
      {
        _connection_table = value;
      }
      get
      {
        return _connection_table;
      }
    }

  /**
   * @param p the AHPacket to route
   * @param from The edge the packet came from
   * @param deliverlocally set to true if the local node should Announce it
   * @return the number of edges the packet it Sent on.
   */
    public int Route(Edge from, AHPacket p, out bool deliverlocally)
    {
      RwtaAddress dest = (RwtaAddress) p.Destination;
      int size = _connection_table.Count(ConnectionType.Unstructured);
      deliverlocally = false;

      if (p.Hops == p.Ttl) {
        deliverlocally = true;
        return 0;  //do nothing      
      }
      else if (p.Hops > p.Ttl) {
        return 0;  //do nothing
      }
      else {
        if (size <= 1) {
          deliverlocally = true;
          return 0;
        }
        else {
          Edge e =_connection_table.GetRandomUnstructuredEdge(from);
          
          if (e!=null) {
            e.Send( p.IncrementHops() );
            return 1;  //packet is routed to only one edge
          } 
          else {
            return 0;
          }
        }
      }
    }

  }

}
