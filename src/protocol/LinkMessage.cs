/*
 * Brunet.AddressParser
 * Brunet.Address;
 * Brunet.ConnectionMessage;
 * Brunet.ConnectionType;
 * Brunet.TransportAddress;
 */

using System.Xml;

using Brunet;

namespace Brunet
{

  /**
   * Link messages are exchanged between hosts as
   * part of a connection forming handshake.
   * The local and remote transport
   * addresses are exchanged in order to help nodes
   * identify when they are behind a NAT, which is
   * translating their IP addresses and ports.
   */

  public class LinkMessage:ConnectionMessage
  {

    /**
     * Use this constructor if you prefer to set
     * each Property using its accessor function
     */
    public LinkMessage()
    {

    }
    public LinkMessage(ConnectionType t,
                       TransportAddress local,
                       TransportAddress remote, Address lnode)
    {
      ConnectionType = t;
      LocalTA = local;
      RemoteTA = remote;
      LocalNode = lnode;
    }

    public LinkMessage(System.Xml.XmlElement link_element)
    {

      foreach(XmlNode attr in link_element.Attributes) {
        switch (attr.Name) {
        case "type":
          /*@throw ArgumentNullException for Enum.Parse if typeof(
           * ConnectionType) or attr.FirstChild.Value is a null reference
           * @throw ArguementException if typeof(ConnectionType) is not 
           * a Type that describes Enum OR attr.FirstChild.Value is
           * either equal to Empty or contains only white space. OR 
           * attr.FirstChild.Value represents one or more names, and 
           * at least one name represents by attr.FirstChild.Value is 
           * not of type typeof(ConnectionType).
           */
          ConnectionType =
            (ConnectionType) System.Enum.Parse(typeof(ConnectionType),
                                               attr.FirstChild.Value,
                                               true);
          break;
        }
      }

      //Read the addresses
      foreach(XmlNode nodes in link_element.ChildNodes) {
        switch (nodes.Name) {
        case "local":
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "host") {
              foreach(XmlNode add in sub.ChildNodes) {
                if (add.Name == "address") {
                  LocalTA =
                    new TransportAddress(add.FirstChild.Value);
                }
              }
            }
            else if (sub.Name == "node") {
              foreach(XmlNode add in sub.ChildNodes) {
                if (add.Name == "address") {
                  LocalNode =
                    AddressParser.Parse(add.FirstChild.Value);
                }
              }
            }
          }
          break;
        case "remote":
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "host") {
              foreach(XmlNode add in sub.ChildNodes) {
                if (add.Name == "address") {
                  RemoteTA =
                    new TransportAddress(add.FirstChild.Value);
                }
              }
            }
          }
          break;
        }
      }
    }

    /* These are attributes in the <link/> tag */
    public ConnectionType ConnectionType;

    public TransportAddress LocalTA;
    public TransportAddress RemoteTA;

    public Address LocalNode;

    override public void WriteTo(XmlWriter w)
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
      w.WriteStartAttribute("type", ns);
      /*@throw InvalidOperationException for WriteString if the
       * WriteState is Closed.
       */
      w.WriteString(this.ConnectionType.ToString().ToLower());
      /*@throw InvalidOperationException for WriteEndAttribute if the
       * WriteState is Closed.
       */
      w.WriteEndAttribute();
      //@throw InvalidOperationException for all the Write* below

      w.WriteStartElement("local", ns); //<local>
      w.WriteStartElement("host", ns);  //<host>
      w.WriteStartElement("address", ns);       //<address>
      w.WriteString(LocalTA.ToString());
      w.WriteEndElement();      //</address>
      w.WriteEndElement();      //</host>
      w.WriteStartElement("node", ns);  //<node>
      w.WriteStartElement("address", ns);       //<address>
      w.WriteString(LocalNode.ToString());
      w.WriteEndElement();      //</address>
      w.WriteEndElement();      //</node>
      w.WriteEndElement();      //</local>

      w.WriteStartElement("remote", ns);        //<remote>
      w.WriteStartElement("host", ns);  //<host>
      w.WriteStartElement("address", ns);       //<address>
      w.WriteString(RemoteTA.ToString());
      w.WriteEndElement();      //</address>
      w.WriteEndElement();      //</host>
      w.WriteEndElement();      //</remote>

      //end of the link element :
      w.WriteEndElement();      //</link>
      w.WriteEndElement();      //</(request|response)>
    }
  }

}
