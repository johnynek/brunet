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

/*
 * Dependencies : 
 Brunet.Address;
 Brunet.AHAddress;
 Brunet.AHPacket;
 Brunet.BigInteger;
 Brunet.ConnectionType;
 Brunet.ConnectionTable
 Brunet.Edge;
 Brunet.EdgeException
 Brunet.IRouter
 */

// #define DEBUG

using System.Collections;
using System;

namespace Brunet
{

  /**
   * Finds the Edge to deliver each AHPacket to.
   * Manages the ordered Address, Edge table which
   * is used to find the Edge which is closest
   * to the destination Address
   *
   * This is only for Structured Addresses, or subclasses
   * of AHAddress
   *
   */

  public class AHRouter:IRouter
  {

    //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(AHRouter));


    /**
     * _local is the current AHAddress of the node of this AHRoutingTable
     */
    protected AHAddress _local;

    /**
     * @param a the address of the node in which the AHRoutingTable is located
     */
    public AHRouter(AHAddress local)
    {
      _local = local;
    }

    protected ConnectionTable _connection_table;
    public ConnectionTable ConnectionTable
    {
      set
      {
        _connection_table = value;
      }
    }

    /**
     * @param dest destination node
     * @param deliverlocally if the local node should recieve a copy, set true
     * @return null if there is no remote destination to get the packet. 
     * 	     edge with AHAdress closest to des
     * @note it returns closest edge even if deliverlocally is true.
     * @todo make sure the world does not end if two people have the
     * same address
     */
    public Edge FindClosestTo(AHAddress dest, out bool deliverlocally)
    {
      //Is this for us?
      if (dest.Equals(_local)) {
        deliverlocally = true;
        return null;
      }
      ArrayList edge_list = null;
      int index = 0, index_s = 0;
      /**
       * Look through all the Connections to see if we can get it directly
       * to the endpoint : 
       */
      lock( _connection_table.SyncRoot ) {
        foreach(ConnectionType t in Enum.
                GetValues(typeof(ConnectionType))) {
          if (_connection_table.Count(t) > 0) {
            //Get the index for this destination
            index = _connection_table.IndexOf(t, dest);
            if (index >= 0) {
              //We can route the packet directly to the end :
              Address a;
              Edge e;
              _connection_table.GetConnection(t, index, out a, out e);
              deliverlocally = false;
              return e;
            }
          }
          else {
            //There are no connections of this type
          }
        }
      }
      if ( _connection_table.Count(ConnectionType.Structured) == 0 ) {
        /** There is no one to route to */
        deliverlocally = true;  /* I guess we are the closest */
        return null;
      }
      /**
       * Otherwise we need to route it to the next closest, using structured
       * connections
       */
      //Search for the nearest node :
      Address next_a = null;
      Edge next_e = null;
      Address add_a = null,  add_b = null;
      Edge edge_a = null,  edge_b = null;
      //Here we do one big atomic operation to get the appropriate edges from
      //the table
      lock(_connection_table.SyncRoot) {
        index = _connection_table.IndexOf(ConnectionType.Structured, dest);
        if( index >= 0) {
          _connection_table.GetConnection(ConnectionType.Structured,
                                          index,
                                          out next_a,
                                          out next_e);
        }
        else {
          //We need to find the closest one :
          int index_a = ~index;
          /* GetConnection "wraps around" so
           * we don't need to worry about going off the end
           */
          int index_b = index_a - 1;


          _connection_table.GetConnection(ConnectionType.Structured,
                                          index_a,
                                          out add_a,
                                          out edge_a);
          _connection_table.GetConnection(ConnectionType.Structured,
                                          index_b,
                                          out add_b,
                                          out edge_b);
        }
      }
      //Now that we have done our interaction with the connection
      //table, we see what the result was:

      if( next_e == null ) {
        //We need to do more work:
        BigInteger dist_a = dest.DistanceTo((AHAddress)add_a).abs();
        BigInteger dist_b = dest.DistanceTo((AHAddress)add_b).abs();

        if ( dist_a < dist_b ) {
          //A is closer
          next_a = add_a;
          next_e = edge_a;
        }
        else if ( dist_a > dist_b ) {
          //B is closer
          next_a = add_b;
          next_e = edge_b;
        }
        else {
          //They are the same magnitude, pick the one on the opposite side
          //of the destination from us, which is further away:
          BigInteger da = _local.DistanceTo((AHAddress)add_a).abs();
          BigInteger db = _local.DistanceTo((AHAddress)add_b).abs();
          if( da > db ) {
            //Choose A
            next_a = add_a;
            next_e = edge_a;
          }
          else {
            //Choose B
            next_a = add_b;
            next_e = edge_b;
          }
        }
      }
      BigInteger next_dist = dest.DistanceTo((AHAddress)next_a).abs();
      BigInteger our_dist = dest.DistanceTo(_local).abs();
      //deliverlocally = (our_dist <= next_dist);

      // if we are closer to the destination than any of our neighbors
      // we must deliver the packet locally and deliver it to the neighbor
      // that is closest to the destination and on the opposite side from
      // it i.e. if the destination is to our left we also deliver to our
      // left neighbor on the structured ring and if the destination is to
      // our right we also deliver to our neighbor on the right.
      if( our_dist <= next_dist ) {
        deliverlocally = true;
        if( dest.IsLeftOf( _local ) ) {
          next_e = _connection_table.GetLeftStructuredNeighborOf(_local);
        }
        else {
          next_e = _connection_table.GetRightStructuredNeighborOf(_local);
        }
      }
      else {
        deliverlocally = false;
      }

      return next_e;
    }

    /**
     * @param dest destination node
     * @param from the edge we got the packet from
     * @param deliverlocally if the local node should recieve a copy, set true
     * @return the edge to send it to.  Null if there is none closer
     */
    public Edge FindClosestTo(AHAddress dest,
                              Edge from, out bool deliverlocally)
    {
      Edge closest_edge = FindClosestTo(dest, out deliverlocally);

      if (closest_edge != null && closest_edge == from) {
        /**
        * we got the packet from the edge which is closest
        * to the destination.  The spec says that we deliver
        * locally in this case.
        */
        deliverlocally = true;
        return null;
      }
      else {
        return closest_edge;
      }
    }

    public System.Type RoutedAddressType
    {
      get
      {
        return typeof(AHAddress);
      }
    }

    public int Route(Edge e, AHPacket p, out bool deliverlocally)
    {
      Edge next = null;
      deliverlocally = false;
      try {
        /* Find the edge to send the packet to */
        AHAddress dest = (AHAddress) p.Destination;
        next = FindClosestTo(dest, e, out deliverlocally);
        /* This is the ttl == hops rule */
        deliverlocally |= (p.Hops == p.Ttl);
        int sent = 0;
        if ((next != null) && (p.Hops < p.Ttl)) {
          /* If we send it, set sent to 1 */
          next.Send( p.IncrementHops() );
          sent++;
          #if DEBUG
          System.Console.WriteLine("Routing on edge {0} packet: {1}", next.ToString(), p.ToString());
          System.Console.ReadLine();
          #endif    
        }
        return sent;
      }
      catch(EdgeException ex) {
        //log.Error("Tried to send to a closed edge",ex);
        next.Close();   /* Close the edge when there is a problem */
        return -1;
      }
      catch(Exception x) {
        /* The destination must have not been a AHAdress */
        //log.Error("AHRouter: exception",x);
        return -1;
      }

    }
  }


}
