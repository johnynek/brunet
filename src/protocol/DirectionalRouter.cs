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
 * Brunet.AHAddressComparer
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

    public System.Type RoutedAddressType
    {
      get
      {
        return typeof(DirectionalAddress);
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
          int count =  _con_tab.Count(ConnectionType.Structured);
          if (count > 0) {
            //In this case we send it to an address based
            //on its position in our routing table :
            //Find ourselves in the routing table
            int index = _con_tab.IndexOf(ConnectionType.Structured, _local);
            Address next_add;
            if (dir_add.Bearing == DirectionalAddress.Direction.Left) {
              if (index < 0) {
                //Since we are going Left, we choose the larger address,
                //which is where index already is
                index = ~index;
              }
              else {
                //Since we are going Left, we choose the larger address
                index++;
              }
              /* Get the Edge for this index */
              _con_tab.GetConnection(ConnectionType.Structured,
                                     index,
                                     out next_add,
                                     out next);
              if( from == next ) {
                //We skip where we came from:
                index++;
                _con_tab.GetConnection(ConnectionType.Structured,
                                       index,
                                       out next_add,
                                       out next);
              }
            }
            else if (dir_add.Bearing == DirectionalAddress.Direction.Right) {
              //Find ourselves in the routing table
              if (index < 0) {
                index = ~index;
              }
              /*
              * No matter what, we decrement the index.  If we are
              * in the table, decrement to go the right.  If we are
              * not in the table, we need to choose the smaller index
              */
              index--;
              /* Get the Edge for this index */
              _con_tab.GetConnection(ConnectionType.Structured,
                                     index,
                                     out next_add,
                                     out next);
              if( from == next ) {
                //We skip where we came from:
                index--;
                _con_tab.GetConnection(ConnectionType.Structured,
                                       index,
                                       out next_add,
                                       out next);
              }
            }
          }
        } /* This is the end of the atomic ConnectionTable operation set */
        //This is the ttl == hops rule
        deliverlocally |= (p.Hops == p.Ttl);
        if ( (next != null)
             && (p.Hops < p.Ttl) //Send only packets with some Ttl left
             && (next != from) ) { //Don't send it the way it came no matter what
          //Increment the hops :
          //If we send it, set sent to 1
          next.Send( p.IncrementHops() );
          sent = 1;
        }
        else {
          //Looks like it is up to us
          deliverlocally = true;
        }
      }
      catch(System.Exception x) {
        //_log.Error("DirectionalRouter exception:", x);
      }
      return sent;
    }
  }

}
