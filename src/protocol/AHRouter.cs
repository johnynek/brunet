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
    protected class CachedRoute {
      public Connection Route;
      public AHAddress Destination;
      public bool DeliverLocally;
      public Edge Previous;

      public CachedRoute(AHAddress dest, Edge prev, Connection route, bool send_local) {
        Route = route;
        Destination = dest;
        DeliverLocally = send_local;
        Previous = prev;
      }
    }
    
    protected Cache _route_cache;
    public AHRouter(AHAddress local)
    {
      _local = local;
      /*
       * Store the 1000 most commonly used routes.
       * Since this may cause us to keep an extra 1000
       * AHAddress objects in memory, each of which requires
       * about 20 bytes, this costs on the order of 10-100 KB
       */
      _route_cache = new Cache(1000);
      _our_left_n = null;
    }
    protected AHAddress _local;
    ///This is our left neighbor.  We often need to look at this.
    protected Connection _our_left_n;

    protected ConnectionTable _tab;
    public ConnectionTable ConnectionTable {
      set {
        if( _tab != null ) {
          //Clear the old events:
          _tab.ConnectionEvent -= this.ConnectionTableChangeHandler;
          _tab.DisconnectionEvent -= this.ConnectionTableChangeHandler;
	  _tab.StatusChangedEvent -= this.StatusChangedHandler;
        }
        _tab = value;
        _tab.ConnectionEvent += this.ConnectionTableChangeHandler;
        _tab.DisconnectionEvent += this.ConnectionTableChangeHandler;
	
	//new stuff added to ensure we dont get into problem of
	//unequal references for objects that are otherwise equal
	_tab.StatusChangedEvent += this.StatusChangedHandler;
      }
    }
    protected static readonly int _MAX_UPHILL_HOPS = 1; 
    /**
     * The type of address this class routes
     */
    public System.Collections.IEnumerable RoutedAddressClasses { get { return new int[]{0}; } }
   
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
#if AHROUTER_DEBUG
      bool debug = false;
      if (p.PayloadType == AHPacket.Protocol.ReqRep) {
	ReqrepManager.DebugPacket(_local,  p, prev_e);
	debug = true;
      }
#endif
      Connection next_con = null;  //the next connection to send the packet to
      deliverlocally = false;
      
      AHAddress dest = (AHAddress)p.Destination;
      /*
       * The following cases don't require us to consult the Connection table
       */
      short hops = p.Hops;
      short ttl = p.Ttl;
      if( hops > ttl ) {
        //This should never have gotten here:
        System.Console.Error.WriteLine(
             "Bad Packet from: {0}, hops({1}) > ttl({2})", prev_e, hops, ttl);
	return 0;
      }
      else if ( _local.Equals(dest) ) {
        //This packet is for us!  Woo hoo!
	//There is no option that does not mean deliver in this case
	deliverlocally = true;
	//We can stop routing now, no one is closer than us.
#if AHROUTER_DEBUG
	if (debug) {
	  System.Console.WriteLine("Delloc: {0}\n from: {1}\n delloc: {2}",
				   p,prev_e,deliverlocally);
	  System.Console.WriteLine("{0}: We are the destination, WOW!", _local);
	}
#endif
	return 0;
      }
      else if( hops == ttl ) {
        //We are the last to route the packet.
	if( p.HasOption( AHPacket.AHOptions.Last ) ) {
          /*
           * No need to check any routing tables.  We get it
           */
	  deliverlocally = true;
#if AHROUTER_DEBUG
	  if (debug) System.Console.WriteLine("{0}: TTL expired. Still deliverlocally (option Last).", _local);
#endif
	  return 0;
	}
	else {
          //We only deliver it if we are the nearest.
          //We check this below.
	}
      }
      CachedRoute cr = (CachedRoute)_route_cache[dest];
      if( cr != null && (cr.Previous == prev_e) ) {
        //Awesome, we already know the path to this node.
        //This cuts down on latency
        next_con = cr.Route;
        deliverlocally = cr.DeliverLocally;
#if AHROUTER_DEBUG
	if (debug) 
	{
	  if (next_con != null) {
	    System.Console.WriteLine("{0}: We found a cached route. local delivery: {1}, next_con: {2}.",
				     _local, deliverlocally, next_con.Address);
	  } else {
	    System.Console.WriteLine("{0}: We found a cached route. local delivery: {1}, next_con = null.",
				     _local, deliverlocally);
	  }
	}
#endif
      }
      else {
      /*
       * else we know hops < ttl, we can route:
       * We now need to check the ConnectionTable
       */
      /* Don't let the routing table change */
      lock( _tab.SyncRoot ) {
	next_con = _tab.GetConnection(ConnectionType.Leaf, dest);
	if( next_con == null ) {
          /*
	   * We do not have a leaf connection to use, now we must
	   * find a Structured connection over which to route the packet
	   */
#if AHROUTER_DEBUG
	  if (debug) System.Console.WriteLine("{0}: We do not have a leaf connection.", _local);
#endif
          int dest_idx = _tab.IndexOf(ConnectionType.Structured, dest);
          if( dest_idx >= 0 ) {
            //We actually have a connection to this node:
#if AHROUTER_DEBUG
	    if (debug) System.Console.WriteLine("{0}: We have a structured connection to destination.", _local);
#endif
            next_con = _tab.GetConnection(ConnectionType.Structured, dest_idx);
          }
          else if( _tab.Count(ConnectionType.Structured) == 0 ) {
            //We don't have any structured connections.  I guess we are the closest:
            deliverlocally = true;
            next_con = null;
          }
          else {
            //dest_idx is not in the table:

#if AHROUTER_DEBUG
	    if (debug) System.Console.WriteLine("{0}: We do not have a structured connection to destination.", 
				     _local);
#endif
            dest_idx = ~dest_idx;
            /*
             * Here are the right and left neighbors of the destination
             * left is increasing, right is decreasing.
             * Remember the ConnectionTable wraps around, so no need to worry
             * about the size of index
             */
            int left_idx = dest_idx;
            Connection left_n = _tab.GetConnection(ConnectionType.Structured, left_idx);
#if AHROUTER_DEBUG
	    if (debug && left_n != null) System.Console.WriteLine("{0}: key left connection: {1}.",
						_local, left_n.Address);
#endif
	    
            int right_idx = dest_idx - 1;
            Connection right_n = _tab.GetConnection(ConnectionType.Structured, right_idx);
#if AHROUTER_DEBUG
	    if (debug && right_n != null) System.Console.WriteLine("{0}: key right connection: {1}.",
	                                                         _local, right_n.Address);
#endif
		     
            //We check the a couple of connections:
            BigInteger l_dist = dest.DistanceTo((AHAddress)left_n.Address).abs();
            BigInteger r_dist = dest.DistanceTo((AHAddress)right_n.Address).abs();
            Connection closest_con;
            Connection other_con;
            BigInteger closest_dist;
            BigInteger other_dist;
            if( l_dist < r_dist ) {
              closest_con = left_n;
              other_con = right_n;
              closest_dist = l_dist;
              other_dist = r_dist;
#if AHROUTER_DEBUG
	      if (debug)  System.Console.WriteLine("{0}: Going the left way (since it is closer).", _local);
#endif
            }
            else {
              closest_con = right_n;
              other_con = left_n;
              closest_dist = r_dist;
              other_dist = l_dist;
#if AHROUTER_DEBUG
	      if (debug) System.Console.WriteLine("{0}: Going the right way (since it is closer).", _local);
#endif
            }
            /**
             * Here we consider the various routing modes
             */
            if( p.HasOption( AHPacket.AHOptions.Greedy ) ) {
#if AHROUTER_DEBUG
	      if (debug) System.Console.WriteLine("{0}: Greedy routing mode.", _local);
#endif
              /*
               * We pass it ONLY IF we can get it closer than we are.
               */
              BigInteger our_dist = dest.DistanceTo(_local).abs();
              if( closest_dist < our_dist ) {
                if( closest_con.Edge != prev_e ) {
#if AHROUTER_DEBUG
		  if (debug)  System.Console.WriteLine("{0}: Greedy. Closest distance is lesser than our distance.", 
					   _local);
#endif
	          next_con = closest_con;
                }
                else {
#if AHROUTER_DEBUG
		  if (debug)  System.Console.WriteLine("Got wrong greedy packet from: {0}", prev_e);
#endif

                  //This should never happen, a buggy client must have given
                  //us a packet they shouldn't have:
                  System.Console.Error.WriteLine("Got wrong greedy packet from: {0}", prev_e);
                  next_con = null;
                }
	        deliverlocally = false;
	      }
	      else {
                //We keep it.
#if AHROUTER_DEBUG
		if (debug)  System.Console.WriteLine("{0}: Closest distance not lesser than us. Lets keep it.", 
					 _local);
#endif
                next_con = null;
	        deliverlocally = true;
	      }
	    }
            else {
#if AHROUTER_DEBUG
	      if (debug)  System.Console.WriteLine("{0}: Annealing routing mode.", _local);
#endif
              //All the other routing modes use the Annealing rule
              
              /*
               * If we are to the left or right of the destination, and
               * we are not in the table, as we should not be, then we share a common
               * left neighbor with the destination
               */
              if( _our_left_n == null ) {
                /*
                 * Compute our left neighbor.  We only need to do this when it
                 * has changed.
                 */
                int our_idx = _tab.IndexOf(ConnectionType.Structured, _local);
                if( our_idx < 0 ) {
                  our_idx = ~our_idx;
                }
                else {
                  System.Console.Error.WriteLine(
                    "ERROR: we are in the ConnectionTable: {0}", _local);
                }
                _our_left_n = _tab.GetConnection(ConnectionType.Structured, our_idx);
              }
#if AHROUTER_DEBUG
	      if (debug) {
		if (_our_left_n != null) {
		  System.Console.WriteLine("{0}: our left connection: {1}", _local, _our_left_n.Address);
		} else {
		  System.Console.WriteLine("{0}: our left connection: null");
		}
		try {
		  System.Console.WriteLine("{0}: Testing == between: {1} and {2}, equality: {3}", 
					   _local, left_n.Address, _our_left_n.Address, 
					   (left_n == _our_left_n));
		  System.Console.WriteLine("{0}: Operand 1, hashcode: {1}, tostring(): {2}",
					   _local, left_n.GetHashCode(), left_n);
		  System.Console.WriteLine("{0}: Operand 2, hashcode: {1}, tostring(): {2}",
					   _local, _our_left_n.GetHashCode(), _our_left_n);
		  System.Console.WriteLine("{0}: Hashcode equality: {1}", _local, (left_n.GetHashCode() == _our_left_n.GetHashCode()));
		} catch(System.Exception e) {
		  System.Console.WriteLine("{0}: excption in debugging code!", _local); 
		}
	      }
#endif
	      
              if( left_n == _our_left_n ) {
#if AHROUTER_DEBUG
		if (debug)  System.Console.WriteLine("{0}: I am adjacent to the destination (matching neighbors)", _local);
#endif
                /*
                 * We share a common left neighbor, so we should deliver locally
                 * This is the only case where we should deliver locally,
                 * otherwise there is at least one node on either side of the
                 * target, so one of them should probably get the packet.
                 */
                deliverlocally = true;
#if AHROUTER_DEBUG
		if (debug) System.Console.WriteLine("{0}: Local delivery for sure. Who else gets it.", _local);
#endif
                //The next step should be the node on the "other side"
                if( _local.IsLeftOf( dest ) ) {
                  next_con = right_n;
#if AHROUTER_DEBUG
		  if (debug) {
		    if (next_con != null) {
		      System.Console.WriteLine("{0}: Adjacent, also give to the guy on right: {1}", _local, next_con.Address);
		    } else {
		      System.Console.WriteLine("{0}: Adjacent, also give to the guy on right: null", _local);
		    }
		  }
#endif
                }
                else {
                  next_con = left_n;
#if AHROUTER_DEBUG
		  if (debug) {
		    if (next_con != null) {
		      System.Console.WriteLine("{0}: Adjacent, also give to the guy on left: {1}", _local, next_con.Address);	 
		    } else {
		      System.Console.WriteLine("{0}: Adjacent, also give to the guy on left: null", _local);	 
		    }
		  }
#endif
                }
                if( prev_e == next_con.Edge ) {
                  //Don't send it back the way it came
#if AHROUTER_DEBUG
		  if (debug) System.Console.WriteLine("{0}: Adjacent, dont send it back", _local);
#endif
                  next_con = null;
                }
              }
              else if ( hops == 0 ) {
                /*
                 * This is the case that we sent the packet, and we are not
                 * a neighbor of the packet (the previous case)
                 * So, the closest_con must be good since we are the source
                 */
                next_con = closest_con;
              }
              else if (hops <= _MAX_UPHILL_HOPS ) {
                /*
                 * We will allow the packet to go uphill (get further from the source)
                 * at first, but this has to stop in order to prevent loops
                 *
                 * This may help the network form in the massive join case, or under
                 * heavy churn. @todo analyze approaches for improving stabilization
                 * in massively disordered cases.
                 */
                if( closest_con.Edge != prev_e ) {
                  //Awesome.  This is an easy case...
                  next_con = closest_con;
                }
                else {
                  /*
                   * Look at the two next closest and choose the minimum distance of
                   * the three
                   */
                  int sc_idx = -1;
                  if( closest_con == right_n ) {
                    //move one over
                    sc_idx = right_idx - 1;
                  }
                  else {
                    //Must be the left:
                    sc_idx = left_idx + 1;
                  }
                  Connection second_closest = _tab.GetConnection(ConnectionType.Structured,
                                                                 sc_idx);
                  BigInteger second_dist =
                                 dest.DistanceTo( (AHAddress)second_closest.Address).abs();
                  if( second_dist < other_dist ) {
                    other_con = second_closest;
                  }
                  if( other_con.Edge != prev_e ) {
                    //If we only have one neighbor,
                    //other and closest might be the same
                    next_con = other_con;
                  }
                  else {
                    //We just can't win...
                    next_con = null;
                  }
                }
              }
              else {
                /*
                 * This is the case where we are not a neighbor of the destination
                 * according to our table, and the packet has taken at least 2 hops.
                 */
                deliverlocally = false;
                if( ( closest_con.Edge == prev_e ) 
                    && ( other_con.Edge != prev_e ) ) {
                  closest_dist = other_dist;
                  closest_con = other_con;
                }
                Connection prev = _tab.GetConnection(prev_e);
                if( prev != null ) {
                  BigInteger prev_dist = dest.DistanceTo( (AHAddress)prev.Address ).abs();
                  if( closest_dist >= prev_dist ) {
                    //Don't send it if you can't get it closer than it was before
                    next_con = null;
                  }
                  else {
                    next_con = closest_con;
                  }
                }
                else {
                  //This is the case that we don't have a connection
                  //on the Edge the packet came from, this shouldn't happen,
                  //but it is not a disaster.
                  next_con = closest_con;
                }
              }//End of non-neareast neighbor case
            }//End of Annealing case
          }//End of the case where we had to find a near route
	}
	else {
          //We can route directly to the destination.
	}
       }//End of ConnectionTable lock
       /*
        * We update the route cache with the most recent Edge to send to
        * that destination.
        */
       _route_cache[dest] = new CachedRoute(dest, prev_e, next_con, deliverlocally);
      }//End of cache check   
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

      /*
       * Now we have next_con if we can send it somewhere closer.
       */
      try {
	if( next_con != null && (hops < ttl) ) {
          //We can send it on
          next_con.Edge.Send( p.IncrementHops() );
#if AHROUTER_DEBUG
	  if (debug) {
	    System.Console.WriteLine("Sending {0}\n from: {1} to: {2}\n delloc: {3}",
				     p,prev_e,next_con,deliverlocally);
	  }
#endif
	  return 1;
	}
	else {
#if AHROUTER_DEBUG
	  if (debug) {
	    System.Console.WriteLine("Not sending {0}\n from: {1}\n delloc: {2}",
				     p,prev_e,deliverlocally);
	  }
#endif
          return 0;
	}
      }
      catch(EdgeException x) {
	System.Console.Error.WriteLine(x);
	System.Console.WriteLine("{0}: Edge exception encountered while sending from: {1} delloc: {2}",
				 _local,prev_e,deliverlocally);
        next_con.Edge.Close();
	return -1;
      }
    }

    /**
     * When the ConnectionTable changes, our cached routes are all trash
     */
    protected void ConnectionTableChangeHandler(object o, System.EventArgs args) {
      _route_cache.Clear();
      //Our left neighbor may have changed:
      _our_left_n = null;
    }
    protected void StatusChangedHandler(object ct, System.EventArgs args) {
      ConnectionEventArgs ce = (ConnectionEventArgs)args;
      Connection new_con = ce.Connection;
      if (_our_left_n != null) {
	if( new_con.Edge == _our_left_n.Edge ) {
	  _our_left_n = new_con;
	}
      } 
    }
  }
}
