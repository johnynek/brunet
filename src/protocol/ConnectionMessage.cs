/**
 * dependencies
 * Brunet.Packet
 * Brunet.ConnectionPacket
 */

using System.IO;
using System.Xml;

namespace Brunet
{

/**
 * A simple request/response protocol for negotiating
 * connections.
 */

  abstract public class ConnectionMessage
  {

  /**
   * A constructor which reads the data message from
   * a XmlElement.  This should just be the element
   * for the message type, not the whole <request />
   * or <response />
   */
    public ConnectionMessage(System.Xml.XmlElement encoded)
    {

    }
    public ConnectionMessage()
    {
    }


    virtual public byte[]  ToByteArray()
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
      return s.ToArray();
    }

    virtual public ConnectionPacket ToPacket()
    {
//Here is a buffer to write the connection message into : 
      MemoryStream s = new MemoryStream(2048);
//This first byte says it is a ConnectionPacket : 
      s.WriteByte((byte) Packet.ProtType.Connection);
      XmlWriter w =
        new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      this.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      w.Close();
      return new ConnectionPacket(s.ToArray());
    }

    /**
     * Implement Object.ToString().  Basically, we
     * write the message into a StringWriter
     */
    override public string ToString()
    {
      return System.Text.Encoding.UTF8.GetString( ToByteArray() );
    }
    
  /**
   * Each message should be able to write themselves out
   */
    virtual public void WriteTo(System.Xml.XmlWriter w)
    {
      string xml_ns = "";
      w.WriteStartElement(Dir.ToString().ToLower(), xml_ns);
      w.WriteStartAttribute("id", xml_ns);
      w.WriteString(Id.ToString());
      w.WriteEndAttribute();
    }

    public enum Direction
    {
      Request,
      Response
    }

    public Direction Dir;
    public int Id;
  }

}
