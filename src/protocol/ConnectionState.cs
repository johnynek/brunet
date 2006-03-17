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

/** The class maintains the ConnectionState for an attempted connection. 
 *  And is used by Connectors to track linkers to a given destination.
 */

namespace Brunet {
  public class ConnectionState {
    /** target we are linking to. */
    protected Address _target;
    
    /** connection type this linker should work on. */
    protected string _ct;
    
    /** readonly accessor methods. */
    public Address Target {
      get {
	return _target;
      }
    }
    public string Type {
      get {
	return _ct;
      }
    }

    public ConnectionState(Address target, string ct) {
      _target = target;
      _ct = ct;
    }
    override public bool Equals(Object other)  
    {
      ConnectionState cs = other as ConnectionState;
      if (cs == null) {
	return false;
      }
      if (Target.Equals(cs.Target) && Type.Equals(cs.Type)) {
	return true;
      }
      return false;
    }
    override public int GetHashCode() {
      return Target.GetHashCode() ^ Type.GetHashCode();
    }
  }
}

