/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

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
      deliverlocally = false;

      if (p.Hops == p.Ttl) {
        deliverlocally = true;
        return 0;  //do nothing
      }
      else if (p.Hops > p.Ttl) {
        return 0;  //do nothing
      }
      else {
          //get a random unstructured edge (if there is one) for the next hop
          Connection temp_con = _connection_table.GetRandom(ConnectionType.Unstructured);
          if (temp_con != null) {
            Edge e = temp_con.Edge;
            e.Send( p.IncrementHops() );
            return 1;  //packet is routed to only one edge
          }
          else {
            /*
             * If we cannot get even one random connection,
             * this means we cannot pass the packet on, so
             * we should process it locally.
             */
            deliverlocally = true;
            return 0;
          }
      }
    }

  }

}
