/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
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
    public ConnectionList CList {
      get { return NewState.GetConnections(Connection.MainType); }
    }
    /**
     * The new Connection
     */ 
    public readonly Connection Connection;
    /// State of the ConnectionTable just before the event
    public readonly ConnectionTableState OldState;
    /// State of the ConnectionTable just after the event
    public readonly ConnectionTableState NewState;
    /**
     * The index into CList that corresponds to CList
     */
    public readonly int Index;
    /** Sequence number to compare order of changes
     */
    public int View { 
      get { return NewState.View; }
    }

    public ConnectionEventArgs(Connection c, int idx,
                               ConnectionTableState old, ConnectionTableState news) {
      Connection = c;
      Index = idx;
      OldState = old;
      NewState = news;
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
