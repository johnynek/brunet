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
      XmlElement ping_element = (XmlElement)r.FirstChild;
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
    
    override public IXmlAble ReadFrom(System.Xml.XmlElement el)
    {
      return new PingMessage(el);
    }
    
    override public void WriteTo(System.Xml.XmlWriter w)
    {
      base.WriteTo(w);  //<(request|response)>

      string xml_ns = "";
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
