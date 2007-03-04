/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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
 * Dependencies : 
 * Brunet.ConnectionMessage
 */
using System.Xml;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * The ping message is sent and acknowledged
   * anytime a node wants to test a connection,
   * and ALWAYS after a successful link transaction
   * (by the initiator).
   */
  public class PingMessage:ConnectionMessage
  {

    public PingMessage()
    {

    }

    public PingMessage(System.Xml.XmlElement r) : base(r)
    {
    }
    public PingMessage(ConnectionMessage.Direction dir, int id, XmlReader r)
    {
      if( !CanReadTag(r.Name) ) {
        throw new ParseException("This is not a <ping /> message");
      }
      this.Dir = dir;
      this.Id = id;
    }

    override public bool CanReadTag(string tag)
    {
      return (tag == "ping");
    }

    override public bool Equals(object o)
    {
      if( o is PingMessage ) {
        return true;
      }
      else {
        return false;
      }
    }
    override public int GetHashCode() {
      return base.GetHashCode();
    }
    
    override public IXmlAble ReadFrom(System.Xml.XmlElement el)
    {
      return new PingMessage(el);
    }

    override public IXmlAble ReadFrom(XmlReader r)
    {
      Direction dir;
      int id;
      ReadStart(out dir, out id, r);
      return new PingMessage(dir, id, r);
    }
    
    override public void WriteTo(System.Xml.XmlWriter w)
    {
      base.WriteTo(w);  //<(request|response)>

      string xml_ns = System.String.Empty;
      w.WriteStartElement("ping", xml_ns);      //<ping>
      w.WriteEndElement();      //</ping>
      w.WriteEndElement();      //</(request|response)>
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class PingMessageTester {
    public PingMessageTester() { }

    [Test]
    public void PMSerializationTest()
    {
      XmlAbleTester xt = new XmlAbleTester();
      
      PingMessage pm1 = new PingMessage();
      PingMessage pm2 = (PingMessage)xt.SerializeDeserialize(pm1);
      Assert.AreEqual(pm1, pm2);
    }
  }
#endif

}
