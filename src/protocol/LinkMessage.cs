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

/*
 * Brunet.AddressParser
 * Brunet.Address;
 * Brunet.ConnectionMessage;
 * Brunet.ConnectionType;
 * Brunet.TransportAddress;
 */

using System.Xml;
using System.Collections.Specialized;
using Brunet;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Link messages are exchanged between hosts as
   * part of a connection forming handshake.
   * The local and remote transport
   * addresses are exchanged in order to help nodes
   * identify when they are behind a NAT, which is
   * translating their IP addresses and ports.
   *
   *
   * This class is immutable
   */

  public class LinkMessage:ConnectionMessage
  {

    public LinkMessage(ConnectionType t,
                       NodeInfo local,
                       NodeInfo remote)
    {
      _attributes = new StringDictionary();
      _attributes["type"] = Connection.ConnectionTypeToString(t);
      _local_ni = local;
      _remote_ni = remote;
    }

    public LinkMessage(string connection_type, NodeInfo local, NodeInfo remote)
    {
      _attributes = new StringDictionary();
      _attributes["type"] = connection_type;
      _local_ni = local;
      _remote_ni = remote;
    }
    public LinkMessage(StringDictionary attributes, NodeInfo local, NodeInfo remote)
    {
      _attributes = attributes;
      _local_ni = local;
      _remote_ni = remote;
    }
    /**
     * Deserializes an entire request which should contain a link element
     */
    public LinkMessage(System.Xml.XmlElement r) : base(r)
    {

      XmlElement link_element = (XmlElement)r.FirstChild;
      _attributes = new StringDictionary();
      foreach(XmlNode attr in link_element.Attributes) {
	_attributes[ attr.Name ] = attr.FirstChild.Value;
      }
      //System.Console.Write("Looking in child nodes");
      //Read the NodeInfo
      foreach(XmlNode nodes in link_element.ChildNodes) {
        if( nodes.Name == "local") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _local_ni = new NodeInfo((XmlElement)sub);
	      //System.Console.Write("Read local");
            }
          }
	}
	else if(nodes.Name == "remote") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _remote_ni = new NodeInfo((XmlElement)sub);
	      //System.Console.Write("Read Remote");
            }
          }
	}
      }
    }

    public LinkMessage(Direction dir, int id, XmlReader r)
    {
      if( !CanReadTag(r.Name) ) {
        throw new ParseException("This is not a <link /> message");
      }
      this.Id = id;
      this.Dir = dir;
      //Read the attributes:
      if( !r.MoveToFirstAttribute() ) {
        throw new ParseException("There is no type for this <link /> message");
      }
      _attributes = new StringDictionary();
      do {
        _attributes[ r.Name ] = r.Value;
      }
      while( r.MoveToNextAttribute() );
      
      if( !_attributes.ContainsKey("type") ) {
        throw new ParseException("There is no type for this <link /> message");
      }
      
      bool finished = false;
      NodeInfo tmp = null;
      while( r.Read() ) {
        /*
	 * We look for the remote and local parts of the
	 * link message:
	 */
	if( r.NodeType == XmlNodeType.Element && r.Name.ToLower() == "node" ) {
          tmp = new NodeInfo(r);
	}
	if( r.NodeType == XmlNodeType.EndElement ) {
          //By now, we must have read the node info
          if( r.Name.ToLower() == "local" ) {
            _local_ni = tmp;
	    tmp = null;
	  }
	  else if( r.Name.ToLower() == "remote" ) {
            _remote_ni = tmp;
	    tmp = null;
	  }
	}
      }
    }

    /* These are attributes in the <link/> tag */
    /**
     * @returns the Main ConnectionType of this message.
     * @todo Make sure the usage of this is consistent
     */
    public ConnectionType ConnectionType {
      get { return Connection.StringToMainType( ConTypeString ); }
    }
    
    protected StringDictionary _attributes;
    public StringDictionary Attributes {
      get { return _attributes; }
    }
    public string ConTypeString { get { return _attributes["type"]; } }

    protected NodeInfo _local_ni;
    public NodeInfo Local {
      get { return _local_ni; }
    }

    protected NodeInfo _remote_ni;
    public NodeInfo Remote {
      get { return _remote_ni; } 
    }

    public override bool CanReadTag(string tag)
    {
      return (tag == "link");
    }
    
    /**
     * @return true if olm is equivalent to this
     */
    public override bool Equals(object olm)
    {
      LinkMessage lm = olm as LinkMessage;
      if ( lm != null ) {
        bool same = true;
	same &= (lm.Attributes.Count == Attributes.Count );
	same &= lm.ConTypeString == ConTypeString;
	if( same ) {
          //Make sure all the attributes match:
	  foreach(string key in lm.Attributes.Keys) {
            same &= lm.Attributes[key] == Attributes[key];
	  }
	}
	same &= lm.Local.Equals(_local_ni);
	same &= lm.Remote.Equals(_remote_ni);
	return same;
      }
      else {
        return false;
      }
    }
   
    public override IXmlAble ReadFrom(XmlElement el)
    {
      return new LinkMessage(el);
    }

    public override IXmlAble ReadFrom(XmlReader r)
    {
      Direction dir;
      int id;
      ReadStart(out dir, out id, r);
      return new LinkMessage(dir, id, r);
    }   
    
    /**
     * Write this object into the XmlWriter w.
     * This method may be used for serialization.
     */
    public override void WriteTo(XmlWriter w)
    {
      base.WriteTo(w);

      string ns = "";           //Xml namespace
      /*@throw InvalidOperationException for WriteStartElement if the WriteState
       * is Closed.
       */
      w.WriteStartElement("link", ns);
      //Write the attributes :
      /*@throw InvalidOperationException for WriteStartAttribute if the
       * WriteState is Closed.
       */
      foreach(string key in _attributes.Keys) {
        w.WriteAttributeString( key, _attributes[key] );
      }
      //@throw InvalidOperationException for all the Write* below

      w.WriteStartElement("local", ns); //<local>
      _local_ni.WriteTo(w);
      w.WriteEndElement();      //</local>
      
      w.WriteStartElement("remote", ns);        //<remote>
      _remote_ni.WriteTo(w);
      w.WriteEndElement();      //</remote>

      //end of the link element :
      w.WriteEndElement();      //</link>
      w.WriteEndElement();      //</(request|response)>
    }
  }

#if BRUNET_NUNIT
//Here are some NUnit 2 test fixtures
  [TestFixture]
  public class LinkMessageTester {

    public LinkMessageTester() { }

    [Test]
    public void LMSerializationTest()
    {
      LinkMessage l1 = new LinkMessage(ConnectionType.Structured,
		                   new NodeInfo(null,
				       new TransportAddress("brunet.tcp://127.0.0.1:45")),
				   new NodeInfo(
				       new DirectionalAddress(DirectionalAddress.Direction.Left),
				       new TransportAddress("brunet.tcp://127.0.0.1:837")) );
      XmlAbleTester xt = new XmlAbleTester();
      LinkMessage l2 = (LinkMessage)xt.SerializeDeserialize(l1);
      //System.Console.WriteLine("\nl1: {0}\n\nl2: {0}\n", l1, l2);
      Assert.AreEqual(l1, l2, "LinkMessage test 1");
    }
  }

#endif

}
