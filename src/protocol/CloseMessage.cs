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
