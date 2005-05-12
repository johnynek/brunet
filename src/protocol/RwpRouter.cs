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
 * Dependencies : 
 * Brunet.Address
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 * Brunet.IRouter
 * Brunet.RwpAddress
 * Brunet.AHPacket
 * Brunet.Edge
 */

using System;
using System.Collections;

namespace Brunet
{

  /**
   * This router only cares about which edges are availible.
   */

  public class RwpRouter:IRouter
  {
    protected static Random _rand =  new Random(DateTime.Now.Millisecond);

    public RwpRouter()
    {

    }

    public System.Type RoutedAddressType
    {
      get
      {
        return typeof(RwpAddress);
      }
    }

    protected ConnectionTable _connection_table;
    public ConnectionTable ConnectionTable
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

    public int Route(Edge from, AHPacket p, out bool deliverlocally)
    {
      /**
       *Each RWP address has two variables, a flag f, and a probability p.
       *First, if the flag is set (f=1), then one of the neighbors is selected 
       *uniformly at random and the packet is forwarded to that node with the
       *same destination address. Additionally, the destination address flag 
       *is flipped to unset (f=0).  Now, for each neighbor (other than the
       *one selected in the first part) we handle the packet as though we 
       *recieved it with (f=0).  When a node recieves a packet with
       *an RWP destination address with f unset, then it sends the packet
       *to each neighbor with probability p.
       *
       * @todo the random number generation is no good in here.
       * 
       */

      RwpAddress add = (RwpAddress) p.Destination;
      float prob = add.Prob;
      bool  flag = add.Flag;
      int   neighbors_size = _connection_table.Count(ConnectionType.Unstructured);

      //Every node in the path processes add packet
      deliverlocally = true;

      if (neighbors_size < 2 || p.Hops >= p.Ttl) {
        return 0; //do nothing
      }
      else {
        int num_sent_on = 0;
        Edge e = null;

        if (flag) {
          //Send this packet to a random neighbor exactly as is;
          e = _connection_table.GetRandomUnstructuredEdge(from);

          if (e != null) {
            AHPacket to_send = p.IncrementHops();
            num_sent_on++;
            e.Send( to_send );
          }
        }

        //Now we send to each neighbor with probability p
        AHPacket next_p = null;
        if( flag ) {
          /*
          * In this case, we have to make a copy of the packet
          * because we have to change it and Packets are immutable
          */ 
          RwpAddress old_dest = (RwpAddress)p.Destination;
          RwpAddress dest = new RwpAddress( false, old_dest.Prob );

          /**
           * Make a new packet from the old one, only changing the header
           */
          next_p = new AHPacket( (short)(p.Hops + 1),
                                 p.Ttl,
                                 p.Source,
                                 dest,
                                 p );
        }
        else {
          /*
          * Just increment the hops
          */
          next_p = p.IncrementHops();
        }
        ArrayList unstructured_edges = (ArrayList)_connection_table.GetEdgesOfType(ConnectionType.Unstructured);
        try {
          foreach(Edge next_edge in unstructured_edges) {
            if ( (next_edge==from) || (next_edge==e) )
              continue;
            else {
              double val = _rand.NextDouble();
              if ( val <= prob ) {
                num_sent_on++;
                next_edge.Send( next_p );
              }
            }
            //end of for-loop
          }
        }
        catch (Exception ex) {
          // log it. do a code review and log the exceptions that should be logged
        }
        return num_sent_on;
      }
    }

  }

}
