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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

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

      XmlElement mess = null;
      //Find the first element :
      foreach(XmlNode n in doc.ChildNodes) {
        if (n is XmlElement) {
          mess = (XmlElement)n;
          break;
        }
      }
      ConnectionMessage result = null;
      
      switch( ConnectionMessage.GetTagOf( mess ) ) {
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
      return result;
    }
  }
#if BRUNET_NUNIT

  [TestFixture]
  public class CMPTester {
    public CMPTester() { }

    [Test]
    public void Test()
    {
      //We make one of each object, then turn it into a byte array, then parse it.
      System.Collections.ArrayList messages = new System.Collections.ArrayList();

      messages.Add(new CloseMessage("huh"));
      messages.Add(new PingMessage());
      messages.Add(new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected, "this is an error"));
      LinkMessage l1 = new LinkMessage(ConnectionType.Structured,
                                   new NodeInfo(null,
                                       new TransportAddress("brunet.tcp://127.0.0.1:45")),
                                   new NodeInfo(
                                       new DirectionalAddress(DirectionalAddress.Direction.Left),
                                       new TransportAddress("brunet.tcp://127.0.0.1:837")) );
      messages.Add(l1);
      
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = new TransportAddress("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);
      ConnectToMessage ctm1 = new ConnectToMessage(ConnectionType.Unstructured, ni);

      messages.Add(ctm1);

      ConnectionMessageParser cmp = new ConnectionMessageParser();
      foreach(ConnectionMessage cm in messages) {
        ConnectionMessage cm2 = cmp.Parse( cm.ToByteArray() );
	Assert.AreEqual(cm, cm2);
      }

    }
  }
  
#endif

}
