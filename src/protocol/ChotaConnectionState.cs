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

/** The class maintains the state of prospective ChotaConnections. 
 *  This is used by ChotaConnectionOverlord to decide if we should make
 *  connection attempt.
 */

namespace Brunet {
  public class ChotaConnectionState {
    /** target we are keeping state about. */
    protected Address _target;

    /** connector associated with the state. */
    protected Connector _con = null;
    
    //boolean flag indicating we got a packet back from the node
    //we initiate ChotaConnections only if we observe bidirectional connectivity
    private bool _received;

    public bool Received {
      get {
	return _received;
      }
      set {
#if ARI_CHOTA_DEBUG
	if (value) {
	  Console.WriteLine("ChotaConnectionState:  Detecting bidirectional connectivity");
	}
#endif
	_received = value;
      }
    }

    
    public Address Address {
      get {
	return _target;
      }
    }

    /** default constructor. */
    public ChotaConnectionState(Address target) {
      _target = target;
    }
 
    /** whether we should make a connection attempt. 
     *  only when there are no active connectors. 
     */
    public bool CanConnect {
      get {
#if ARI_CHOTA_DEBUG
	if (_con != null) {
	  Console.WriteLine("ChotaConnectionState:  Active connector exists. (Don't reattempt)");
	}
	if (!_received) {
	  Console.WriteLine("ChotaConnectionState:  No bidirectional connectivity (Don't reattempt)");
	}
#endif
	if (_con == null && _received) {
	  return true;
	}
	return false;
      }
    }
    /** ChotaConnectionOverlord just created a new connector. 
     *  We keep its state here.
     */
    public Connector Connector {
      set {
	_con = value;
      } 
      get {
	return _con;
      }
    }
  }
}

