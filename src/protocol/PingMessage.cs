/*
 * Dependencies : 
 * Brunet.ConnectionMessage
 */

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

    public PingMessage(System.Xml.XmlElement ping_element)
    {

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

}
