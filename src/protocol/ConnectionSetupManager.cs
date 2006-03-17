/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Collections;

/** The class maintains the ConnectionSetupManager for an 
 *  attempted connection. 
 *  And is used by Connectors (and CtmRequestHandlers) to track linkers to 
 *  given destination for a giver connection type.
 */

namespace Brunet {
  public class ConnectionSetupManager {

    /** an object to lock for thread sync */
    private object _sync;
    
    protected Node _node;

    //record the state of a connection in setup
    protected Hashtable _connection_state;

    //we further have a way to map active linkers to connection states
    protected Hashtable _active_linkers;

    public ConnectionSetupManager(Node n) {
      _sync = new Object();
      lock(_sync) 
	{
	  _node = n;
	  _connection_state = new Hashtable();
	  _active_linkers = new Hashtable();
	}
    }
    
    /** method that determines if there is already an active connection attempt
     *  @param target address of the remote node
     *  @param ct string representing the connection type
     */
    public bool IsActive(Address target, string ct) {
      lock(_sync) {
	if(_connection_state.ContainsKey(new ConnectionState(target, ct)))
	{
#if CNX_SETUP_DEBUG
	  Console.WriteLine("Active connection attempt for: {0};{1}", target, ct);
#endif 
	  return true;
	} else {
#if CNX_SETUP_DEBUG
	  Console.WriteLine("No active connection attempt for: {0};{1}", target, ct);
#endif 
	  return false;
	}
      }
    }

    /** method that decides whether to create a linker or queue 
     *  it up for later.
     *  @param target address we are trying to connect
     *  @param target_list remote TAs for the address
     *  @ct string representation of the connection type
     */
    public void StartLinking(Address target, ICollection target_list, string ct) 
    {

#if CNX_SETUP_DEBUG
      Console.WriteLine("Trying to start linking with target : {0};{1}", target, ct);
#endif 
      Linker new_l = null;
      ConnectionState cs = new ConnectionState(target, 
					       ct);
      LinkerState ls = null;

      lock(_sync) {

	if (_connection_state.ContainsKey(cs)) {
	  //this certainly means that there is an active linker
#if CNX_SETUP_DEBUG
	  Console.WriteLine("Active connection attempt for: {0};{1}", target, ct);
#endif 
	  ls = (LinkerState) _connection_state[cs];
	  //we increment the pending linker count
	  ls.Count++;
#if CNX_SETUP_DEBUG
	  Console.WriteLine("{0} pending connection attempts to: {1}:{2}", cs.Target, 
			    cs.Type, ls.Count); 
#endif 
	  //update the transport endpoints
	  ls.TAs = target_list;
	} else {
#if CNX_SETUP_DEBUG
	  Console.WriteLine("Creating a linker for: {0};{1}", target, ct);
#endif 
	  ls = new LinkerState(target_list);
	  _connection_state[cs] = ls;
	  new_l = new Linker(_node);
	  //setup the reverse mapping as well; so that we know the 
	  //connection state when linker ends in constant time
	  _active_linkers[new_l] = cs;
	}
      }
      if (new_l != null) {
	new_l.FinishEvent += new EventHandler(this.LinkerEndHandler);
	new_l.Link(cs.Target, ls.TAs, cs.Type);
      }
    }
    /** Handles a situation where the linker has finished. 
     *  This involves activating all the pending linkers if they exist
     */
    protected void LinkerEndHandler(object linker, EventArgs args) 
    {
      Linker new_l = null;
      ConnectionState cs = null;
      LinkerState ls = null;

      lock(_sync) {
	Linker end_l = (Linker) linker;
	cs = (ConnectionState) _active_linkers[end_l];

#if CNX_SETUP_DEBUG
	Console.WriteLine("Linker ended for: {0};{1}", cs.Target, cs.Type);
#endif 
	//remove the key from the list
	_active_linkers.Remove(end_l);
	
	//check if we have pending linkers for the connection state
	ls = (LinkerState) _connection_state[cs];
	if (ls.Count == 0) {
	  //its time that we simple discard the connection state
	  _connection_state.Remove(cs);
	  // someone may be interested that a connection attempt has ended conclusively.
	  // this includes the ChotaConnectionOverlord.
	  // so lets just inform them.
#if CNX_SETUP_DEBUG
	  Console.WriteLine("Discarding connection state for target: {0};{1}", cs.Target, 
			    cs.Type);
#endif 
	  return;
	}
	//otherwise create the next linker lined up
	ls.Count--;
#if CNX_SETUP_DEBUG
	Console.WriteLine("{0} pending connection attempts to: {1}:{2}", cs.Target, 
			  cs.Type, ls.Count); 
#endif 
	new_l = new Linker(_node);
	
	//finally reset the mapping for active linkers
	_active_linkers[new_l] = cs;
      }
      
#if CNX_SETUP_DEBUG
      Console.WriteLine("Creating a linker for: {0};{1}", cs.Target, cs.Type);
#endif 
      //this is something we try to keep outside the lock construct
      if (new_l != null) {
	new_l.FinishEvent += new EventHandler(this.LinkerEndHandler);
	//now consider the connection state to find out
	new_l.Link(cs.Target, ls.TAs, cs.Type);
      }
    }
  }
}

