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
 * Dependencies
 * Brunet.Address
 * Brunet.AHPacket
 * Brunet.ConnectionTable
 * Brunet.ConnectionType
 * Brunet.DirectionalAddress
 * Brunet.Edge
 * Brunet.IRouter
 */

using System.Collections;

namespace Brunet
{

  /**
   * Implements Directional routing on the structured system
   * This is used mostly for making connections to near neighbors
   * in the structured system
   */
  public class DirectionalRouter:IRouter
  {
    /*private static readonly log4net.ILog _log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    public DirectionalRouter(Address a)
    {
      _local = a;
    }

    public IEnumerable RoutedAddressClasses
    {
      get
      {
        return new int[]{ 124 };
      }
    }

    protected Address _local;
    protected ConnectionTable _con_tab;
    public ConnectionTable ConnectionTable
    {
      set
      {
        _con_tab = value;
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
      Edge next = null;
      deliverlocally = false;
      int sent = 0;

      /* We can only route if it is a DirectionalAddress and we have
       * structured connections
       */
      try {
        DirectionalAddress dir_add = (DirectionalAddress) p.Destination;
        /* We need to do a few atomic operations on the ConnectionTable */
        lock( _con_tab.SyncRoot ) {
          Connection next_con = null;
          if ( dir_add.Bearing == DirectionalAddress.Direction.Left ) {
            //Get the left structured neighbor of us:
            next_con = _con_tab.GetLeftStructuredNeighborOf((AHAddress)_local);
            if( next_con.Edge == from ) {
              //skip the person it came from
              AHAddress f_add = (AHAddress)next_con.Address;
              next_con = _con_tab.GetLeftStructuredNeighborOf(f_add);
            }
          }
          else if (dir_add.Bearing == DirectionalAddress.Direction.Right) {
            //Get the left structured neighbor of us:
            next_con = _con_tab.GetRightStructuredNeighborOf((AHAddress)_local);
            if( next_con.Edge == from ) {
              //skip the person it came from
              AHAddress f_add = (AHAddress)next_con.Address;
              next_con = _con_tab.GetRightStructuredNeighborOf(f_add);
            }
          }
          if (next_con != null ) {
            //Here is the edge to go to next
            next = next_con.Edge;
          }
        } /* This is the end of the atomic ConnectionTable operation set */
	if( p.HasOption( AHPacket.AHOptions.Path ) ) {
          deliverlocally = true;
	}
	else if( p.HasOption( AHPacket.AHOptions.Last ) ) {
          if( next == null || p.Hops == p.Ttl ) {
            deliverlocally = true;
	  }
	}
        if ( (next != null)
             && (p.Hops < p.Ttl) //Send only packets with some Ttl left
             && (next != from) ) { //Don't send it the way it came no matter what
          //Increment the hops :
          //If we send it, set sent to 1
          next.Send( p.IncrementHops() );
          sent = 1;
        }
      }
      catch(System.Exception x) {
        //_log.Error("DirectionalRouter exception:", x);
      }
      return sent;
    }
  }

}
