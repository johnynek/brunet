/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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
using System.IO;
using System.Xml;
using System.Collections;
#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Parses and Serializes a ConnectionMessage or ConnectionPacket.
   */

  public class ConnectionMessageParser
  {

#if DOM
    protected XmlDocument doc;
#endif
    
    public ConnectionMessageParser()
    {
#if DOM
      doc = new XmlDocument();
#endif
      _parsed_packets = null;
    }
    public ConnectionMessageParser(Node n) {
      lock( _node_to_wht ) {
        _parsed_packets = (WeakHashtable)_node_to_wht[n];
        if( _parsed_packets == null ) {
          _parsed_packets = new WeakHashtable();
          _node_to_wht[n] = _parsed_packets;
        }
      }
    }
    /*
     * An ArrayList of Parsed Packets;
     */
    protected WeakHashtable _parsed_packets;
    
    /**
     * We can keep a per node cache of parsed
     * packets, this keeps them for all the nodes
     */
    static protected Hashtable _node_to_wht;

    static ConnectionMessageParser() {
      _node_to_wht = new Hashtable();
    }
    /**
     * Parse the payload of the given packet
     * @param p Packet whose payload we parse
     * @return the ConnectionMessage inside
     * @throws ParseException if we cannot parse
     */
    public ConnectionMessage Parse(MemBlock p)
    {
      /*
       * Parsing is expensive.  Packets are immutable.  So,
       * we keep a weakreference to each packet we have already
       * parsed.
       */
      ConnectionMessage result = null;
      if( _parsed_packets != null ) {
        //We have a cache...
        lock( _parsed_packets ) {
          result = (ConnectionMessage)_parsed_packets[p];
          if( result == null ) {
            result = Parse(p.ToMemoryStream());
            _parsed_packets[p] = result;
          }
        }
      }
      else {
        //No cache..
        result = Parse(p.ToMemoryStream());
      }
      //Console.Error.WriteLine("Parsed: {0}\n{1}\n",p, result);
      return result;
    }

    public ConnectionMessage Parse(byte[] bin)
    {
      return Parse(bin, 0, bin.Length);
    }

    public ConnectionMessage Parse(byte[] binary, int offset, int l)
    {
      Stream s = new MemoryStream(binary, offset, l);
      return Parse(s);
    }
    public ConnectionMessage Parse(Stream s)
    {
#if DOM
      doc.Load(s);
      return Parse(doc);
#else
      XmlReader r = new XmlTextReader(s);
      return Parse(r);
#endif
    }

    /**
     * Parse the message contained in the string
     * @param s the string containing the message
     * @return the ConnectionMessage contained in s
     */
    public ConnectionMessage Parse(string s)
    {
#if DOM
      doc.Load(new StringReader(s));
      return Parse(doc);
#else
      XmlReader r = new XmlTextReader(new StringReader(s));
      return Parse(r);
#endif
    }
#if DOM
    static public ConnectionMessage Parse(XmlDocument doc)
    {
      //Now we have the ConnectionMessage in memory as a DOM document

      XmlElement mess = null;
      //Find the first element :
      foreach(XmlNode n in doc.ChildNodes) {
        if (n is XmlElement) {
          mess = (XmlElement)n;
          break;
        }
      }
      ConnectionMessage result = null;
      
      switch( ConnectionMessage.GetTagOf( mess ) ) {
          case "connectTo":
            result = new ConnectToMessage((XmlElement) mess);
            break;
          case "link":
            result = new LinkMessage((XmlElement) mess);
            break;
          case "close":
            result = new CloseMessage((XmlElement) mess);
            break;
          case "ping":
            result = new PingMessage((XmlElement) mess);
            break;
          case "error":
            result = new ErrorMessage((XmlElement) mess);
            break;
	  case "status":
	    result = new StatusMessage(mess);
	    break;
          default:
            throw new
            ParseException("Unknown ConnectionMessage Type: " +
                           mess.Name);
      }
      return result;
    }
#endif
    static public ConnectionMessage Parse(XmlReader r)
    {
      ConnectionMessage result = null;
      ConnectionMessage.Direction dir;
      int id;
      ConnectionMessage.ReadStart(out dir, out id, r);
      switch( r.Name ) {
          case "connectTo":
            result = new ConnectToMessage(dir, id, r);
            break;
          case "link":
            result = new LinkMessage(dir, id, r);
            break;
          case "close":
            result = new CloseMessage(dir, id, r);
            break;
          case "ping":
            result = new PingMessage(dir, id, r);
            break;
          case "error":
            result = new ErrorMessage(dir, id, r);
            break;
	  case "status":
	    result = new StatusMessage(dir, id, r);
	    break;
          default:
            throw new
            ParseException("Unknown ConnectionMessage Type: " + r.Name);
      }
      return result;
    }

  }
#if BRUNET_NUNIT

  [TestFixture]
  public class CMPTester {
    public CMPTester() { }

    [Test]
    public void Test()
    {
      //We make one of each object, then turn it into a byte array, then parse it.
      System.Collections.ArrayList messages = new System.Collections.ArrayList();

      messages.Add(new CloseMessage("huh"));
      messages.Add(new PingMessage());
      messages.Add(new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected, "this is an error"));
      //Add a Link Message
      LinkMessage l1 = new LinkMessage(ConnectionType.Structured,
                                   new NodeInfo(null,
                                       TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:45")),
                                   new NodeInfo(
                                       new DirectionalAddress(DirectionalAddress.Direction.Left),
                                       TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:837")) );
      messages.Add(l1);
      
      //At a ConnectToMessage:
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);
      ConnectToMessage ctm1 = new ConnectToMessage(ConnectionType.Unstructured, ni);

      messages.Add(ctm1);
      
      //Add a StatusMessage:
      System.Collections.ArrayList neighbors = new System.Collections.ArrayList();
      for(int i = 5001; i < 5010; i++) {
        neighbors.Add(new NodeInfo(new DirectionalAddress(DirectionalAddress.Direction.Left),
				  TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:"
					  + i.ToString())));
      }
      StatusMessage statm = new StatusMessage("structured", neighbors);
      messages.Add(statm);
      
      ConnectionMessageParser cmp = new ConnectionMessageParser();
      foreach(ConnectionMessage cm in messages) {
        ConnectionMessage cm2 = cmp.Parse( cm.ToByteArray() );
	Assert.AreEqual(cm, cm2);
        Packet p = cm.ToPacket();
        ConnectionMessage cm3 = cmp.Parse(p.Payload);
        //Do this twice to test caching:
        ConnectionMessage cm4 = cmp.Parse(p.Payload);
        Assert.AreEqual(cm, cm3);
        Assert.AreEqual(cm, cm4);
      }
      //Here are some string tests:
      string close_string = "<request id=\"12\"><close>Byebye</close></request>";
      CloseMessage cs = new CloseMessage("Byebye");
      cs.Id = 12;
      cs.Dir = ConnectionMessage.Direction.Request;
      Assert.AreEqual(cs, cmp.Parse(close_string), "CloseMessage string test");
      string error_string = "<response id=\"345\"><error code=\"18\">already</error></response>";
      ErrorMessage es = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,"already");
      Assert.AreEqual(es, cmp.Parse(error_string), "ErrorMessage string test");

      string ctm_string = "<response id=\"1491988272\"><connectTo type=\"structured\">"
	                  +"<node address=\"brunet:node:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO\">"
			  + "<transport>brunet.tcp://127.0.0.1:20293/</transport></node></connectTo>"
			  + "</response>";
      Address ctma = AddressParser.Parse("brunet:node:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO");
      ConnectToMessage ctm_s = new ConnectToMessage(ConnectionType.Structured,
		                                    new NodeInfo(ctma,
					TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:20293/") ) );
      Assert.AreEqual(ctm_s, cmp.Parse(ctm_string), "ConnectToMessage string test");

      string lm_string = "<request id=\"42\"><link type=\"structured.near\" realm=\"pobland\">"
	                + "<local><node address=\"brunet:node:XXXAAAAAAAAAAAAAAAAAAAAAAAAAAAAO\">"
			  + "<transport>brunet.tcp://127.0.0.1:2000/</transport></node></local>"
			  + "<remote><node address=\"brunet:node:YYYAAAAAAAAAAAAAAAAAAAAAAAAAAAAO\">"
			  + "<transport>brunet.tcp://127.0.0.1:2001/</transport></node></remote>"
			  + "</link></request>";
      Address lma = AddressParser.Parse("brunet:node:XXXAAAAAAAAAAAAAAAAAAAAAAAAAAAAO");
      Address lmb = AddressParser.Parse("brunet:node:YYYAAAAAAAAAAAAAAAAAAAAAAAAAAAAO");
      StringDictionary attrs = new StringDictionary();
      attrs["type"] = "structured.near";
      attrs["realm"] = "pobland";
      LinkMessage lm_s = new LinkMessage(attrs,
		                         new NodeInfo(lma,
						      TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:2000/") ),
					 new NodeInfo(lmb,
						      TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:2001/") ) );
      
      Assert.AreEqual( lm_s, cmp.Parse(lm_string) );
      string tun_lm_string = "<?xml version=\"1.0\" encoding=\"utf-8\"?><request id=\"1\"><link type=\"structured.chota\" realm=\"ari_dht\"><local><node address=\"brunet:node:UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22\"><transport>brunet.tunnel://UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/FE4QWASN</transport></node></local><remote><node address=\"brunet:node:IJVH4C5PXTHEGLNNKAHAI667VX47UMA6\"><transport>brunet.tunnel://IJVH4C5PXTHEGLNNKAHAI667VX47UMA6/FE4QWASN</transport></node></remote></link></request>";
      LinkMessage tun_lm = (LinkMessage) cmp.Parse(tun_lm_string);
      Assert.AreEqual(tun_lm.ToString(), tun_lm_string);
    }
  }
  
#endif

}
