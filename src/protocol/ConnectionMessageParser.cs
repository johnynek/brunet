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
      //Now we have the ConnectionMessage in memory as a DOM document

      XmlNode r = doc.FirstChild;
      //Find the first element :
      foreach(XmlNode n in doc.ChildNodes) {
        if (n is XmlElement) {
          r = n;
          break;
        }
      }
      //Parse the direction :
      ConnectionMessage.Direction d =
        (ConnectionMessage.Direction) System.Enum.
        Parse(typeof(ConnectionMessage.Direction), r.Name, true);
      //The outer node should be an XmlElement with an "id" attribute :
      int id = 0;
      foreach(XmlNode attr in((XmlElement) r).Attributes) {
        if (attr.Name == "id") {
          //The child of the attribute is a XmlText :
          id = System.Int32.Parse(attr.FirstChild.Value);
          break;
        }
      }
      //Now we have the direction and the id for the request/response

      ConnectionMessage result = null;

      foreach(XmlNode mess in r.ChildNodes) {
        if (mess is XmlElement) {
          switch (mess.Name) {
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
          default:
            throw new
            ParseException("Unknown ConnectionMessage Type: " +
                           mess.Name);
          }
        }
      }
      //We have not yet set the id and direction, this is the same for all
      result.Id = id;
      result.Dir = d;
      return result;
    }
  }

}
