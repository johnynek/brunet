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

//#define AHROUTER_DEBUG

namespace Brunet
{

  /**
   * Router for some of the structured addresses
   */
  public class AHRouter : IRouter
  {

    public AHRouter(AHAddress local)
    {
      _local = local; 
    }
    protected AHAddress _local;

    protected ConnectionTable _tab;
    public ConnectionTable ConnectionTable { set { _tab = value; } }
    
    /**
     * The type of address this class routes
     */
    public System.Type RoutedAddressType { get { return typeof(AHAddress); } }
   
    /**
     * Route the packet p which came from edge e, and set
     * deliverlocally to true if this packet should be delivered
     * to the local node.
     *
     * The routing algorithm can be summarized:
     * <ol>
     * <li>If Hops <= 1, route closest to the dest, other than previous, else:</li>
     * <li>If the closest is closer than the previous, route to closest, else:</li>
     * <li>stop</li>
     * </ol>
     * 
     * Local delivery is done anytime there is either no next hop,
     * or the next hop is further from the destination than we are.
     * 
     * @return the number of edges we send the packet to
     */
    public int Route(Edge prev_e, AHPacket p, out bool deliverlocally)
    {
      Connection next_con = null;  //the next connection to send the packet to
      deliverlocally = false;
      
      AHAddress dest = p.Destination as AHAddress;
      /*
       * The following cases don't require us to consult the Connection table
       */
      if( dest == null ) {
	//This is not our kind of address
        return 0;
      }
      else if( p.Hops > p.Ttl ) {
        //This should never have gotten here:
	return 0;
      }
      else if ( _local.Equals(dest) ) {
        //This packet is for us!  Woo hoo!
	//There is no option that does not mean deliver in this case
	deliverlocally = true;
	//We can stop routing now, no one is closer than us.
#if AHROUTER_DEBUG
        System.Console.WriteLine("Delloc: {0}\n from: {1}\n delloc: {2}",
                            p,prev_e,deliverlocally);
#endif
	return 0;
      }
      else if( p.Hops == p.Ttl ) {
        //We are the last to route the packet.
	if( p.HasOption( AHPacket.AHOptions.Last ) ) {
          /*
           * No need to check any routing tables.  We get it
           */
	  deliverlocally = true;
	  return 0;
	}
	else {
          //We only deliver it if we are the nearest.
          //We check this below.
	}
      }
      /*
       * else we know p.Hops < p.Ttl, we can route:
       * We now need to check the ConnectionTable
       */
      
      /* Don't let the routing table change */
      lock( _tab.SyncRoot ) {
	   
        //See if we can use a leaf connection:
        foreach(Connection c in _tab.GetConnections(ConnectionType.Leaf)) {
          if( c.Address.Equals(dest) ) {
            //We can route it to this 
	    next_con = c;
	  }
	}
	if( next_con == null ) {
          /*
	   * We do not have a leaf connection to use, now we must
	   * find a Structured connection over which to route the packet
	   */
          Connection prev = null;
	  BigInteger prev_dist = Address.Full; //Maximum distance
          prev = _tab.GetConnection(prev_e);
          if( prev != null ) {
	    prev_dist = dest.DistanceTo( (AHAddress)prev.Address).abs();
	  }
          BigInteger our_dist = dest.DistanceTo(_local).abs();

	  BigInteger closest_dist = Address.Full;
	  Connection closest_con = null;
          //We could not use a leaf connection, now check structured:
          foreach(Connection c in _tab.GetConnections(ConnectionType.Structured)) {
	    if ( c != prev ) {
              //This is not the previous.
	      BigInteger this_dist = dest.DistanceTo( (AHAddress)c.Address).abs();
	      if( this_dist < closest_dist ) {
                closest_dist = this_dist;
		closest_con = c;
	      }
	    }
	  }
	  /**
	   * Here we consider the various routing modes
	   */
	  if( p.HasOption( AHPacket.AHOptions.Greedy ) ) {
            if( p.Hops <= 1 || closest_dist < our_dist ) {
              //We pass it on
	      next_con = closest_con;
	    }
	    else {
              //We keep it.
	      deliverlocally = true;
	    }
	  }
          //else if( p.HasOption( AHPacket.AHOptions.Annealing ) )
          else {
            //All the other routing modes use the Annealing rule
            if( p.Hops <= 1 || closest_dist < prev_dist ) {
              //We pass it on
	      next_con = closest_con;
	      if( our_dist < closest_dist ) {
                //The local minima get the packet
	        deliverlocally = true;
	      }
	    }
            else {
              //We don't pass it on, but we may deliver it locally:
              if( our_dist < prev_dist ) {
                deliverlocally = true;
              }
            }
	  }
          //Here are the other modes:
          if( p.HasOption( AHPacket.AHOptions.Last ) ) {
            if( next_con == null ) {
              deliverlocally = true;
            }
            else {
              deliverlocally = false;
            }
          }
          else if( p.HasOption( AHPacket.AHOptions.Path ) ) {
            deliverlocally = true;
          }
          else if( p.HasOption( AHPacket.AHOptions.Exact ) ) {
            if( _local.Equals(dest) ) {
              deliverlocally = true;
              next_con = null;
            }
            else {
              deliverlocally = false;
            }
          }
	}
	else {
          //We can route directly to the destination.
	}
      }//End of ConnectionTable lock

      /*
       * Now we have next_con if we can send it somewhere closer.
       */
      try {
	if( next_con != null && (p.Hops < p.Ttl) ) {
          //We can send it on
          next_con.Edge.Send( p.IncrementHops() );
#if AHROUTER_DEBUG
          System.Console.WriteLine("Sending {0}\n from: {1} to: {2}\n delloc: {3}",
                            p,prev_e,next_con,deliverlocally);
#endif
	  return 1;
	}
	else {
#if AHROUTER_DEBUG
          System.Console.WriteLine("Not sending {0}\n from: {1}\n delloc: {2}",
                            p,prev_e,deliverlocally);
#endif
          return 0;
	}
      }
      catch(EdgeException x) {
	System.Console.Error.WriteLine(x);
        next_con.Edge.Close();
	return -1;
      }
    }
  }
	
}
