/*
 * Dependencies : 
 Brunet.Address
 Brunet.AddressParser
 Brunet.ConnectionMessage
 Brunet.ConnectionType
 Brunet.TransportAddress
 */

using System.Xml;
using System.Collections;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * The ConnectionMessage that is sent out on the network
   * to request connections be made to the sender.
   *
   * When a Node sends out a ConnectToMessage, it puts
   * itself as the target.  This is because that node
   * is requesting that the recipient of the ConnectToMessage
   * connect to the sender (thus the sender is the target).
   *
   * When a node recieves a ConnectToMessage, the CtmRequestHandler
   * processes the message.  ConnectToMessages are sent by
   * Connector objects.
   *
   * This object is immutable
   * 
   * @see CtmRequestHandler
   * @see Connector
   */
  public class ConnectToMessage:ConnectionMessage
  {

    /**
     * @param t connection type
     * @param target the Address of the target node
     */
    public ConnectToMessage(ConnectionType t, NodeInfo target)
    {
      _ct = t;
      _target_ni = target;
    }
    /**
     * Prefer this constructor
     * @param t ConnectionType for this message
     * @param target the Address of the Node to connect to
     * @param tas the TransportAddresses to connect to in order of preference
     */
    public ConnectToMessage(ConnectionType t, Address target, TransportAddress[] tas)
    {
      _ct = t;
      _target_ni = new NodeInfo(target, new ArrayList(tas));
    }
    /**
     * This constructor wraps the above constructor
     * @param t ConnectionType for this message
     * @param target the Address of the Node to connect to
     * @param tas the TransportAddresses to connect to in order of preference
     */
    public ConnectToMessage(ConnectionType t, Address target, ICollection tas)
    {
      _ct = t;
      _target_ni = new NodeInfo(target, new ArrayList(tas));
    }
    /**
     * Deserializes the whole <request />
     */
    public ConnectToMessage(System.Xml.XmlElement r) : base(r)
    {
      XmlElement encoded = (XmlElement)r.FirstChild;
      ArrayList ta_list = new ArrayList();
      //Read the attributes of the connectTo
      foreach(XmlNode attr in((XmlElement) encoded).Attributes)
      {
        switch (attr.Name) {
        case "type":
          _ct =
            (ConnectionType) System.Enum.Parse(typeof(ConnectionType),
                                               attr.FirstChild.Value,
                                               true);
          break;
        }
      }
      //Read the children
      foreach(XmlNode nodes in encoded.ChildNodes)
      {
        if( nodes.Name == "node" ) {
          _target_ni = new NodeInfo((XmlElement)nodes);
	  break;
	}
      }
    }

    protected ConnectionType _ct;
    public ConnectionType ConnectionType { get { return _ct; } }

    protected NodeInfo _target_ni;
    public NodeInfo Target {
      get { return _target_ni; }
    }

    public override bool CanReadTag(string tag)
    {
      return tag == "connectTo";
    }

    public override bool Equals(object o)
    {
      ConnectToMessage co = o as ConnectToMessage;
      if( co != null ) {
        bool same = true;
	same &= co.ConnectionType == _ct;
	same &= co.Target.Equals( _target_ni );
	return same;
      }
      else {
        return false;
      }
    }
    
    public override IXmlAble ReadFrom(XmlElement el)
    {
      return new ConnectToMessage(el);
    }
    
    public override void WriteTo(XmlWriter w)
    {
      //Write the request or response and id
      base.WriteTo(w);  //<(request|response)>

      string ns = "";
      //Here we write out the specific stuff :
      w.WriteStartElement("connectTo", ns);     //<connectTo>
      //Write the attributes :
      w.WriteStartAttribute("type", ns);
      w.WriteString(ConnectionType.ToString().ToLower());
      w.WriteEndAttribute();
      //Write the NodeInfo
      _target_ni.WriteTo(w); 
      //end the connectTo element
      w.WriteEndElement();      //</connectTo>
      w.WriteEndElement();      //</(request|response)>
    }

  }
//Here are some Unit tests:
#if BRUNET_NUNIT
//Here are some NUnit 2 test fixtures
  [TestFixture]
  public class ConnectToMessageTester {

    public ConnectToMessageTester() { }

    [Test]
    public void CTMSerializationTest()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = new TransportAddress("brunet.tcp://127.0.0.1:5000"); 
      NodeInfo ni = new NodeInfo(a, ta);
      ConnectToMessage ctm1 = new ConnectToMessage(ConnectionType.Unstructured, ni);
      XmlAbleTester xt = new XmlAbleTester();
      
      ConnectToMessage ctm1a = (ConnectToMessage)xt.SerializeDeserialize(ctm1);
      Assert.AreEqual(ctm1, ctm1a, "CTM with 1 TA");

      //Test multiple tas:
      ArrayList tas = new ArrayList();
      tas.Add(ta);
      for(int i = 5001; i < 5010; i++)
        tas.Add(new TransportAddress("brunet.tcp://127.0.0.1:" + i.ToString()));
      NodeInfo ni2 = new NodeInfo(a, tas);

      ConnectToMessage ctm2 = new ConnectToMessage(ConnectionType.Structured, ni2);
      
      ConnectToMessage ctm2a = (ConnectToMessage)xt.SerializeDeserialize(ctm2);
      Assert.AreEqual(ctm2, ctm2a, "CTM with 10 TAs");
    }
  }

#endif

}
