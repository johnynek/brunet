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
#endif
using System;
using System.Collections;
using System.Xml;

namespace Brunet {

  /**
   * Holds status information for nodes.  Exchanged
   * in the linking process.  @see Linker, ConnectionMessageHandler
   */
  public class StatusMessage : ConnectionMessage {

    /**
     * Make a status message containing:
     * @param neighbortype what type of connections exist to these neighbors
     * @param neighbors an ArrayList of NodeInfo objects
     */
    public StatusMessage(string neighbortype, ArrayList neighbors)
    {
      _neigh_ct = neighbortype;
      _neighbors = neighbors;
    }

    public StatusMessage(ConnectionType ct, ArrayList neighbors)
    {
      _neigh_ct = Connection.ConnectionTypeToString(ct);
      _neighbors = neighbors;
    }
	  
    public StatusMessage(XmlElement r) : base(r)
    {
      XmlElement status_el = (XmlElement)r.FirstChild;

      foreach(XmlNode cn in status_el.ChildNodes) {
        if( cn.Name == "neighbors" ) {
          foreach(XmlNode attr in cn.Attributes) {
            if ( attr.Name == "type" )  {
              _neigh_ct = attr.FirstChild.Value;
	    }
	  }
          //Read the neighbors:
          _neighbors = new ArrayList();
	  foreach(XmlNode cnkids in cn.ChildNodes) {
            if( cnkids.Name == "node" ) {
	      _neighbors.Add( new NodeInfo((XmlElement)cnkids) );
	    }
	  }
	}
      }
    }

    public StatusMessage(ConnectionMessage.Direction dir, int id, XmlReader r)
    {
      if( !CanReadTag(r.Name) ) {
        throw new ParseException("This is not a <status /> message");
      }
      this.Id = id;
      this.Dir = dir;

      while( r.Read() ) {
        if( r.NodeType == XmlNodeType.Element ) {
	  if( r.Name == "node" ) {
            //Here comes a node info!
	    _neighbors.Add( new NodeInfo(r) );
	  }
	  else if( r.Name == "neighbors" ) {
            _neigh_ct = r["type"];
	    _neighbors = new ArrayList();
	  }
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
    /**
     * Returns an ArrayList of NodeInfo objects for the neighbors
     */
    public ArrayList Neighbors {
      get {
        if( _neighbors == null ) {
          return new ArrayList();
        }
        else {
          return _neighbors;
        }
      }
    }

    public override bool CanReadTag(string tag)
    {
      return (tag == "status");
    }

    /**
     * @return true if osm is equivalent to this object
     */
    public override bool Equals(object osm)
    {
      bool same = base.Equals(osm);
      if (!same) { return false; }
      StatusMessage sm = osm as StatusMessage;
      if( sm != null ) {
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
      return base.GetHashCode() ^ _neighbors.Count;
    }

    public override IXmlAble ReadFrom(XmlElement el)
    {
      return new StatusMessage(el);
    }

    public override IXmlAble ReadFrom(XmlReader r)
    {
      Direction dir;
      int id;
      ReadStart(out dir, out id, r);

      return new StatusMessage(dir, id, r);
    }

    public override void WriteTo(XmlWriter w)
    {
      base.WriteTo(w);
      string ns = String.Empty; //Xml namespace;

      w.WriteStartElement("status", ns); //<status>
      w.WriteStartElement("neighbors", ns); //<neighbors>
      //Here is the type=" " attribute:
      w.WriteStartAttribute("type", ns);
      w.WriteString( _neigh_ct );
      w.WriteEndAttribute();
      //Now for the neighbor list:
      foreach(NodeInfo ni in _neighbors) {
        ni.WriteTo(w);
      }
      w.WriteEndElement(); //</neighbors>
      w.WriteEndElement(); //</status>
      w.WriteEndElement(); //</(request|response)>
    }
  }
#if BRUNET_NUNIT
  /**
   * An NUnit2 TestFixture to test serialization.
   */
  [TestFixture]
  public class StatusMessageTester {
    public StatusMessageTester() { }

    [Test]
    public void SMTest()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);
      
      //Test with one neighbor:
      ArrayList neighbors = new ArrayList();
      neighbors.Add(ni);
      StatusMessage sm1 = new StatusMessage(ConnectionType.Structured, neighbors);
      XmlAbleTester xt = new XmlAbleTester();
      StatusMessage sm1a = (StatusMessage)xt.SerializeDeserialize(sm1);
      Assert.AreEqual(sm1, sm1a, "Single neighbor test");
      //System.Console.WriteLine("\n{0}\n", sm1);
      //Test with many neighbors:
        
      for(int i = 5001; i < 5010; i++) {
        neighbors.Add(new NodeInfo(a,
				  TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:"
					  + i.ToString())));
      }
      StatusMessage sm2 = new StatusMessage(ConnectionType.Unstructured, neighbors);
      StatusMessage sm2a = (StatusMessage)xt.SerializeDeserialize(sm2);
      Assert.AreEqual(sm2,sm2a, "10 Neighbor test");
      //System.Console.WriteLine("\n{0}\n", sm2);
     
      //Here is a StatusMessage with no neighbors (that has to be a possibility)
      StatusMessage sm3 = new StatusMessage("structured", new ArrayList());
      StatusMessage sm3a = (StatusMessage)xt.SerializeDeserialize(sm3);
      Assert.AreEqual(sm3,sm3a, "0 Neighbor test");
      //System.Console.WriteLine("\n{0}\n", sm3);

    }
  }

#endif
}
