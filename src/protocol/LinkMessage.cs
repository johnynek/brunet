/*
 * Brunet.AddressParser
 * Brunet.Address;
 * Brunet.ConnectionMessage;
 * Brunet.ConnectionType;
 * Brunet.TransportAddress;
 */

using System.Xml;

using Brunet;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Link messages are exchanged between hosts as
   * part of a connection forming handshake.
   * The local and remote transport
   * addresses are exchanged in order to help nodes
   * identify when they are behind a NAT, which is
   * translating their IP addresses and ports.
   *
   *
   * This class is immutable
   */

  public class LinkMessage:ConnectionMessage
  {

    public LinkMessage(ConnectionType t,
                       NodeInfo local,
                       NodeInfo remote)
    {
      ConnectionType = t;
      _local_ni = local;
      _remote_ni = remote;
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

      //Read the NodeInfo
      foreach(XmlNode nodes in link_element.ChildNodes) {
        if( nodes.Name == "local") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _local_ni = new NodeInfo((XmlElement)sub);
            }
          }
	}
	else if(nodes.Name == "remote") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _remote_ni = new NodeInfo((XmlElement)sub);
            }
          }
	}
      }
    }

    /* These are attributes in the <link/> tag */
    public ConnectionType ConnectionType;

    protected NodeInfo _local_ni;
    public NodeInfo Local {
      get { return _local_ni; }
    }

    protected NodeInfo _remote_ni;
    public NodeInfo Remote {
      get { return _remote_ni; } 
    }

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
      _local_ni.WriteTo(w);
      w.WriteEndElement();      //</local>
      
      w.WriteStartElement("remote", ns);        //<remote>
      _remote_ni.WriteTo(w);
      w.WriteEndElement();      //</remote>

      //end of the link element :
      w.WriteEndElement();      //</link>
      w.WriteEndElement();      //</(request|response)>
    }
  }

#if BRUNET_NUNIT
//Here are some NUnit 2 test fixtures
  [TestFixture]
  public class LinkMessageTester {

    public LinkMessageTester() { }

    [Test]
    public void SerializationTest() {
      
    }
  }

#endif

}
