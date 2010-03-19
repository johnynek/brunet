/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
using Brunet.Transport;
using Brunet.Symphony;
#endif
using System;
using System.Text;
using System.Collections;
using System.Collections.Specialized;

namespace Brunet.Connections {

  /**
   * Holds status information for nodes.  Exchanged
   * in the linking process.  @see Linker, ConnectionMessageHandler
   */
  public class StatusMessage {

    /**
     * Make a status message containing:
     * @param neighbortype what type of connections exist to these neighbors
     * @param neighbors an ArrayList of NodeInfo objects
     */
    public StatusMessage(string neighbortype, ArrayList neighbors)
    {
      _neigh_ct = String.Intern(neighbortype);
      _neighbors = neighbors;
    }

    public StatusMessage(ConnectionType ct, ArrayList neighbors)
    {
      _neigh_ct = Connection.ConnectionTypeToString(ct);
      _neighbors = neighbors;
    }

    /**
     * Initialize from a Hashtable containing all the information
     */
    public StatusMessage(IDictionary ht) {
      IDictionary neighborinfo = ht["neighbors"] as IDictionary;
      if( neighborinfo != null ) {
        _neigh_ct = String.Intern( neighborinfo["type"] as String );
        IList nodes = neighborinfo["nodes"] as IList;
        if( nodes != null ) {
          _neighbors = new ArrayList(nodes.Count);
          foreach(IDictionary nih in nodes) {
            _neighbors.Add(NodeInfo.CreateInstance(nih));
          }
        }
        else {
          _neighbors = new ArrayList(1);
        }
      }
    }
	  
    protected string _neigh_ct;
    /**
     * The status message holds at most one neighbor tag,
     * this is the type of neighbors: (it must be the same as
     * the type of the connection)
     */
    public string NeighborType {
      get { return _neigh_ct; }
    }
    
    protected ArrayList _neighbors;
    protected static ArrayList EmptyList = new ArrayList(0);
    /**
     * Returns an ArrayList of NodeInfo objects for the neighbors
     */
    public ArrayList Neighbors {
      get {
        if( _neighbors == null ) {
          return EmptyList;
        }
        else {
          return _neighbors;
        }
      }
    }

    /**
     * @return true if osm is equivalent to this object
     */
    public override bool Equals(object osm)
    {
      if( osm == this ) { return true; }
      StatusMessage sm = osm as StatusMessage;
      if( sm != null ) {
        bool same = true;
	same &= _neigh_ct == sm.NeighborType;
	same &= _neighbors.Count == sm.Neighbors.Count;
	if(same) {
          for( int i = 0; i < _neighbors.Count; i++) {
            same &= _neighbors[i].Equals( sm.Neighbors[i] );
	  }
	}
	return same;
      }
      else {
        return false;
      }
    }
    public override int GetHashCode() {
      return _neighbors.Count;
    }

    public IDictionary ToDictionary() {
      ListDictionary neighborinfo = new ListDictionary();
      if( _neigh_ct != null ) {
        neighborinfo["type"] = _neigh_ct;
      }
      if( _neighbors != null ) {
        ArrayList nodes = new ArrayList();
        foreach(NodeInfo ni in _neighbors) {
          nodes.Add( ni.ToDictionary() );
        }
        neighborinfo["nodes"] = nodes;
      }
      ListDictionary ht = new ListDictionary();
      ht["neighbors"] = neighborinfo;
      return ht;
    }

    protected string ToString(string t, IList l) {
      StringBuilder sb = new StringBuilder();
      sb.Append(t + ": ");
      foreach(object o in l) {
        if( o is IDictionary ) {
          sb.Append( ToString(String.Empty, (IDictionary)o) );
        }
        else if (o is IList ) {
          sb.Append( ToString(String.Empty, (IList)o) );
        }
        else {
          sb.Append(o.ToString() + ", ");
        }
      }
      return sb.ToString();
    }
    protected string ToString(string t, IDictionary d) {
      StringBuilder sb = new StringBuilder();
      sb.Append(t + ": ");
      foreach(DictionaryEntry de in d) {
        if( de.Value is IDictionary ) {
          sb.Append( ToString(de.Key.ToString(), (IDictionary)de.Value) );
        }
        else if (de.Value is IList ) {
          sb.Append( ToString(de.Key.ToString(), (IList)de.Value) );
        }
        else {
          sb.Append( de.Key + " => " + de.Value );
        }
      }
      return sb.ToString();
    }
    public override string ToString() {
      return ToString("StatusMessage", ToDictionary());
    }
  }
#if BRUNET_NUNIT
  /**
   * An NUnit2 TestFixture to test serialization.
   */
  [TestFixture]
  public class StatusMessageTester {
    public StatusMessageTester() { }
    
    public void RoundTripHT(StatusMessage sm) {
     StatusMessage sm2 = new StatusMessage( sm.ToDictionary() );
     Assert.AreEqual(sm, sm2, "Hashtable RT");
    }
    [Test]
    public void SMTest()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = NodeInfo.CreateInstance(a, ta);
      
      //Test with one neighbor:
      ArrayList neighbors = new ArrayList();
      neighbors.Add(ni);
      StatusMessage sm1 = new StatusMessage(ConnectionType.Structured, neighbors);
      RoundTripHT(sm1);
      //Console.Error.WriteLine("\n{0}\n", sm1);
      //Test with many neighbors:
        
      for(int i = 5001; i < 5010; i++) {
        neighbors.Add(NodeInfo.CreateInstance(a,
				  TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:"
					  + i.ToString())));
      }
      StatusMessage sm2 = new StatusMessage(ConnectionType.Unstructured, neighbors);
      RoundTripHT(sm2);
      //Console.Error.WriteLine("\n{0}\n", sm2);
     
      //Here is a StatusMessage with no neighbors (that has to be a possibility)
      StatusMessage sm3 = new StatusMessage("structured", new ArrayList());
      RoundTripHT(sm3);
      //Console.Error.WriteLine("\n{0}\n", sm3);

    }
  }

#endif
}
