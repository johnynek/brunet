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
 * Brunet.AHAddress;
 * Brunet.AddressParser;
 * Brunet.ConnectionType;
 * Brunet.ConnectionPacket;
 * Brunet.ConnectionMessage;
 * Brunet.ConnectToMessage;
 * Brunet.CloseMessage;
 * Brunet.ErrorMessage
 * Brunet.LinkMessage;
 * Brunet.Packet;
 * Brunet.ParseException
 * Brunet.PingMessage
 * Brunet.TransportAddress;
 */

using System.IO;
using System.Xml;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Parses and Serializes a ConnectionMessage or ConnectionPacket.
   */

  public class ConnectionMessageParser
  {

    protected XmlDocument doc;

    public ConnectionMessageParser()
    {
      doc = new XmlDocument();
    }

    /**
     * Parse the payload of the given packet
     * @param p Packet whose payload we parse
     * @return the ConnectionMessage inside
     * @throws ParseException if we cannot parse
     */
    public ConnectionMessage Parse(Packet p)
    {
      return Parse( p.PayloadStream );
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
      doc.Load(s);
      return Parse(doc);
    }

    /**
     * Parse the message contained in the string
     * @param s the string containing the message
     * @return the ConnectionMessage contained in s
     */
    public ConnectionMessage Parse(string s)
    {
      doc.Load(new StringReader(s));
      return Parse(doc);
    }
    
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
                                       new TransportAddress("brunet.tcp://127.0.0.1:45")),
                                   new NodeInfo(
                                       new DirectionalAddress(DirectionalAddress.Direction.Left),
                                       new TransportAddress("brunet.tcp://127.0.0.1:837")) );
      messages.Add(l1);
      
      //At a ConnectToMessage:
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = new TransportAddress("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);
      ConnectToMessage ctm1 = new ConnectToMessage(ConnectionType.Unstructured, ni);

      messages.Add(ctm1);
      
      //Add a StatusMessage:
      System.Collections.ArrayList neighbors = new System.Collections.ArrayList();
      for(int i = 5001; i < 5010; i++) {
        neighbors.Add(new NodeInfo(new DirectionalAddress(DirectionalAddress.Direction.Left),
				  new TransportAddress("brunet.tcp://127.0.0.1:"
					  + i.ToString())));
      }
      StatusMessage statm = new StatusMessage("structured", neighbors);
      messages.Add(statm);
      
      ConnectionMessageParser cmp = new ConnectionMessageParser();
      foreach(ConnectionMessage cm in messages) {
        ConnectionMessage cm2 = cmp.Parse( cm.ToByteArray() );
	Assert.AreEqual(cm, cm2);
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
					new TransportAddress("brunet.tcp://127.0.0.1:20293/") ) );
      Assert.AreEqual(ctm_s, cmp.Parse(ctm_string), "ConnectToMessage string test");

      string lm_string = "<request id=\"42\"><link type=\"structured.near\">"
	                + "<local><node address=\"brunet:node:XXXAAAAAAAAAAAAAAAAAAAAAAAAAAAAO\">"
			  + "<transport>brunet.tcp://127.0.0.1:2000/</transport></node></local>"
			  + "<remote><node address=\"brunet:node:YYYAAAAAAAAAAAAAAAAAAAAAAAAAAAAO\">"
			  + "<transport>brunet.tcp://127.0.0.1:2001/</transport></node></remote>"
			  + "</link></request>";
      Address lma = AddressParser.Parse("brunet:node:XXXAAAAAAAAAAAAAAAAAAAAAAAAAAAAO");
      Address lmb = AddressParser.Parse("brunet:node:YYYAAAAAAAAAAAAAAAAAAAAAAAAAAAAO");
      LinkMessage lm_s = new LinkMessage("structured.near",
		                         new NodeInfo(lma,
						      new TransportAddress("brunet.tcp://127.0.0.1:2000/") ),
					 new NodeInfo(lmb,
						      new TransportAddress("brunet.tcp://127.0.0.1:2001/") ) );
      
      Assert.AreEqual( lm_s, cmp.Parse(lm_string) );
						      
    }
  }
  
#endif

}
