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

    /**
     * Deserializes an entire request which should contain a link element
     */
    public LinkMessage(System.Xml.XmlElement r) : base(r)
    {

      XmlElement link_element = (XmlElement)r.FirstChild;
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
      //System.Console.Write("Looking in child nodes");
      //Read the NodeInfo
      foreach(XmlNode nodes in link_element.ChildNodes) {
        if( nodes.Name == "local") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _local_ni = new NodeInfo((XmlElement)sub);
	      //System.Console.Write("Read local");
            }
          }
	}
	else if(nodes.Name == "remote") {
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "node") {
	      _remote_ni = new NodeInfo((XmlElement)sub);
	      //System.Console.Write("Read Remote");
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

    public override bool CanReadTag(string tag)
    {
      return (tag == "link");
    }
    
    /**
     * @return true if olm is equivalent to this
     */
    public override bool Equals(object olm)
    {
      LinkMessage lm = olm as LinkMessage;
      if ( lm != null ) {
        bool same = true;
	same &= lm.ConnectionType == ConnectionType;
	same &= lm.Local.Equals(_local_ni);
	same &= lm.Remote.Equals(_remote_ni);
	return same;
      }
      else {
        return false;
      }
    }
   
    public override IXmlAble ReadFrom(XmlElement el)
    {
      return new LinkMessage(el);
    }
    
    /**
     * Write this object into the XmlWriter w.
     * This method may be used for serialization.
     */
    public override void WriteTo(XmlWriter w)
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
    public void LMSerializationTest()
    {
      LinkMessage l1 = new LinkMessage(ConnectionType.Structured,
		                   new NodeInfo(null,
				       new TransportAddress("brunet.tcp://127.0.0.1:45")),
				   new NodeInfo(
				       new DirectionalAddress(DirectionalAddress.Direction.Left),
				       new TransportAddress("brunet.tcp://127.0.0.1:837")) );
      XmlAbleTester xt = new XmlAbleTester();
      LinkMessage l2 = (LinkMessage)xt.SerializeDeserialize(l1);
      //System.Console.WriteLine("\nl1: {0}\n\nl2: {0}\n", l1, l2);
      Assert.AreEqual(l1, l2, "LinkMessage test 1");
    }
  }

#endif

}
