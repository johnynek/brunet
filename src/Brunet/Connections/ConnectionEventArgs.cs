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

using System.Collections;
using System.Collections.Specialized;
using Brunet.Transport;

namespace Brunet.Connections
{

  /**
   * When a Connection is created, an EventHandler is called
   * with (edge, ConnectionEventArgs) as the parameters
   */

  public class ConnectionEventArgs:System.EventArgs
  {

    public Address RemoteAddress { get { return Connection.Address; } }
    public ConnectionType ConnectionType { get { return Connection.MainType; } }
    public Edge Edge { get { return Connection.Edge; } }
    
    /**
     * This is the ConnectionList that the Connection was
     * inserted into.
     */
    public readonly ConnectionList CList;
    /**
     * The new Connection
     */ 
    public readonly Connection Connection;
    /**
     * The index into CList that corresponds to CList
     */
    public readonly int Index;
    /** Sequence number to compare order of changes
     */
    public readonly int View;

    public ConnectionEventArgs(Connection c, int idx, ConnectionList cl, int view) {
      Connection = c;
      Index = idx;
      CList = cl;
      View = view;
    }

    public IDictionary ToDictionary() {
      ListDictionary ld = new ListDictionary();
      ld.Add("delta", Connection.ToDictionary());
      ld.Add("index", Index);
      ld.Add("cons", CList.ToList());  
      ld.Add("view", View);
      return ld;
    }
  }

}
