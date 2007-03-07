/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Xml;
using System.IO;

#if BRUNET_NUNIT 
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Represents information about a node.  May be exchanged with
   * neighbors or serialized for later usage.
   */
  public class NodeInfo : IXmlAble
  {

    public NodeInfo(System.Xml.XmlElement encoded)
    {
      //Read the address of the node:
      foreach(XmlNode attr in encoded.Attributes) {
        if( attr.Name == "address" ) {
          _address = AddressParser.Parse(attr.FirstChild.Value);
	}
      }
      //Get all the transports:
      _tas = new ArrayList();
      foreach(XmlNode trans in encoded.ChildNodes) {
        if( trans.Name == "transport" ) {
          _tas.Add(TransportAddressFactory.CreateInstance( trans.FirstChild.Value ) );
	}
      }
    }

    public NodeInfo(XmlReader r)
    {
      while( r.NodeType != XmlNodeType.Element ) {
	//Get positioned on the next Element
        if( !r.Read() ) {
          //No more to get:
          throw new ParseException("Cannot find <node />");
	}
      }
      if( !CanReadTag(r.Name) ) {
        throw new ParseException("This is not a <node />");
      }
      string text_add = r["address"];
      if( text_add != null ) {
        _address = AddressParser.Parse( text_add );
      }
      else {
        _address = null;
      }
      _tas = new ArrayList();
      
      bool in_transport = false;
      while( r.Read() ) {
        if( r.NodeType == XmlNodeType.Element && r.Name == "transport" ) {
          in_transport = true;
	}
	else if( r.NodeType == XmlNodeType.Text && in_transport ) {
          //This must be the transport address:
	  _tas.Add(TransportAddressFactory.CreateInstance( r.Value ) );
	}
	else if( r.NodeType == XmlNodeType.EndElement) {
          if( r.Name == "transport" )
            in_transport = false;
	  else if( r.Name == "node" ) {
            //This is the end of this one:
	    break;
	  }
	}
      }
    }

    /**
     * @param a The Address of the node we are refering to
     * @param transports a list of TransportAddress objects
     */
    public NodeInfo(Address a, IList transports)
    {
      _address = a;
      _tas = transports;
    }
    /**
     * We will often create NodeInfo objects with only one
     * TransportAddress.  This constructor makes that easy.
     */
    public NodeInfo(Address a, TransportAddress ta)
    {
      _address = a;
      _tas = new ArrayList();
      _tas.Add(ta);
    }
	  
    protected Address _address;
    /**
     * The address of the node (may be null)
     */
    public Address Address {
      get { return _address; }
    }

    /**
     * The first TransportAddress in the list is used often.
     * This attribute makes it easy to get it.
     * Note that it will also appear as the first position
     * in the Transports list.
     */
    public TransportAddress FirstTA {
      get { return (TransportAddress)_tas[0]; }
    }
    protected IList _tas;
    /**
     * a List of the TransportAddresses associated with this node
     */
    public IList Transports {
      get { return _tas; }
    }

    /**
     * We don't only want to compute the Hash once:
     */
    protected bool _done_hash = false;
    protected int _code;

    /**
     * @return true this is a node tag
     */
    public bool CanReadTag(string tag)
    {
      return (tag == "node");
    }
    
    /**
     * @return true if e is equivalent to this
     */
    public override bool Equals(object e)
    {
      NodeInfo ne = e as NodeInfo;
      if ( ne != null ) {
        bool same = true;
	if (_address != null ) {
	  if ( ne.Address != null ) {
	    same &= _address.Equals(ne.Address);
	  }
	  else {
	    same = false;
	  }
	}
	else {
          same &= ne.Address == null;
	}
	same &= _tas.Count == ne.Transports.Count;
	if( same ) {
	  for(int i = 0; i < _tas.Count; i++) {
            same &= _tas[i].Equals( ne.Transports[i] );
	  }
        }
	return same;
      }
      else {
        return false;
      }
    }

    public override int GetHashCode() {
      if( !_done_hash ) {
        _code = 0;
        if( _address != null ) { _code = _address.GetHashCode(); }
        foreach(TransportAddress ta in _tas) {
          _code ^= ta.GetHashCode();
        }
	_done_hash = true;
      }
      return _code;
    }

    /**
     * @return a NodeInfo read from this element.
     */
    public IXmlAble ReadFrom(XmlElement encoded)
    {
      return new NodeInfo(encoded);
    }
    
    public IXmlAble ReadFrom(XmlReader r)
    {
      return new NodeInfo(r);
    }
    
    override public string ToString()
    {
      //Here is a buffer to write the connection message into :
      MemoryStream s = new MemoryStream(2048);

      XmlWriter w =
        new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      this.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      w.Close();
      return System.Text.Encoding.UTF8.GetString(s.ToArray());
    }
    /**
     * Write into an XmlWriter
     */
    public void WriteTo(System.Xml.XmlWriter w)
    {
      string ns = System.String.Empty;
      w.WriteStartElement("node", ns);//<node>
      if ( _address != null ) {
        w.WriteStartAttribute("address", ns);
	w.WriteString( _address.ToString());
	w.WriteEndAttribute();
      }
      foreach(TransportAddress ta in Transports) {
        w.WriteStartElement("transport",ns);
	w.WriteString(ta.ToString());
	w.WriteEndElement();
      }
      w.WriteEndElement(); //</node>
    }
   
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class NodeInfoTest {
    public NodeInfoTest() {

    }
    //Test methods:
    [Test]
    public void TestWriteAndParse()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);

      XmlAbleTester xt = new XmlAbleTester();

      NodeInfo ni2 = (NodeInfo)xt.SerializeDeserialize(ni);
      //System.Console.WriteLine("n1: {0}\nn2: {1}", ni, ni2);
      Assert.AreEqual(ni, ni2, "NodeInfo: address and 1 ta");
      
      //Test multiple tas:
      ArrayList tas = new ArrayList();
      tas.Add(ta);
      for(int i = 5001; i < 5010; i++)
        tas.Add(TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:" + i.ToString()));
      NodeInfo ni3 = new NodeInfo(a, tas);
      
      ni2 = (NodeInfo)xt.SerializeDeserialize(ni3);
      Assert.AreEqual(ni3, ni2, "NodeInfo: address and 10 tas");

      //Test null address:
      NodeInfo ni4 = new NodeInfo(null, ta);
      
      ni2 = (NodeInfo)xt.SerializeDeserialize(ni4);
      Assert.AreEqual(ni4, ni2, "NodeInfo: null address and 1 ta");

    }
  }
#endif
}
