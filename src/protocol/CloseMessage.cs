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

//using Brunet.ConnectionMessage;
using Brunet;
using System.Xml;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * The close message is sent and acknowledged
   * when a connection is to be closed
   */
  public class CloseMessage:ConnectionMessage
  {

    public CloseMessage()
    {
      _reason = "";
    }
    /**
     * Make a close message with a non-empty reason string
     */
    public CloseMessage(string reason)
    {
      _reason = reason;
    }

    public CloseMessage(XmlElement r) : base(r)
    {
      XmlElement close_element = (XmlElement)r.FirstChild;
      //Get the reason:
      _reason = "";
      if( close_element.FirstChild != null )
        if( close_element.FirstChild.Value != null )
          _reason = close_element.FirstChild.Value;
    }

    protected string _reason;
    public string Reason {
    get { return _reason; }
    }

    override public bool CanReadTag(string tag)
    {
      return (tag == "close");
    }

    override public bool Equals(object o)
    {
      if( o is CloseMessage ) {
        return (((CloseMessage)o).Reason == _reason);
      }
      else {
        return false;
      }
    }
    
    override public IXmlAble ReadFrom(System.Xml.XmlElement el)
    {
      return new CloseMessage(el);
    }
    
    override public void WriteTo(System.Xml.XmlWriter w)
    {

      base.WriteTo(w);  //<(request|response)>
      string xml_ns = "";
      w.WriteStartElement("close", xml_ns);     //<close>
      if( _reason.Length > 0 ) {
        w.WriteString( _reason );
      }
      w.WriteEndElement();      //</close>
      w.WriteEndElement();      //</(request|response)>
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class CloseMessageTester {
    public CloseMessageTester()  { }

    [Test]
    public void CMTest()
    {
      CloseMessage cm1 = new CloseMessage("I've had it");
      XmlAbleTester xt = new XmlAbleTester();
      CloseMessage cm1a = (CloseMessage)xt.SerializeDeserialize(cm1);
      Assert.AreEqual(cm1,cm1a);
    }
  }
  
#endif

}
