using System;
using System.Collections;
using System.Xml;
using System.IO;

#if BRUNET_NUNIT 
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Represents information about a node.  May be exchanged with
   * neighbors or serialized for later usage.
   */
  public class NodeInfo
  {

    public NodeInfo(System.Xml.XmlElement encoded)
    {
      //Read the address of the node:
      foreach(XmlNode attr in encoded.Attributes) {
        if( attr.Name == "address" ) {
          _address = AddressParser.Parse(attr.FirstChild.Value);
	}
      }
      //Get all the transports:
      _tas = new ArrayList();
      foreach(XmlNode trans in encoded.ChildNodes) {
        if( trans.Name == "transport" ) {
          _tas.Add( new TransportAddress( trans.FirstChild.Value ) );
	}
      }
    }

    /**
     * @param a The Address of the node we are refering to
     * @param transports a list of TransportAddress objects
     */
    public NodeInfo(Address a, ArrayList transports)
    {
      _address = a;
      _tas = transports;
    }
    /**
     * We will often create NodeInfo objects with only one
     * TransportAddress.  This constructor makes that easy.
     */
    public NodeInfo(Address a, TransportAddress ta)
    {
      _address = a;
      _tas = new ArrayList();
      _tas.Add(ta);
    }
	  
    protected Address _address;
    /**
     * The address of the node (may be null)
     */
    public Address Address {
      get { return _address; }
    }

    /**
     * The first TransportAddress in the list is used often.
     * This attribute makes it easy to get it.
     * Note that it will also appear as the first position
     * in the Transports list.
     */
    public TransportAddress FirstTA {
      get { return (TransportAddress)_tas[0]; }
    }
    protected ArrayList _tas;
    /**
     * a List of the TransportAddresses associated with this node
     */
    public ArrayList Transports {
      get { return _tas; }
    }

    /**
     * @return true if e is equivalent to this
     */
    public override bool Equals(object e)
    {
      NodeInfo ne = e as NodeInfo;
      if ( ne != null ) {
        bool same = true;
	same &= _address.Equals(ne.Address);
	same &= _tas.Count == ne.Transports.Count;
	if( same ) {
	  for(int i = 0; i < _tas.Count; i++) {
            same &= _tas[i].Equals( ne.Transports[i] );
	  }
        }
	return same;
      }
      else {
        return false;
      }
    }
    override public string ToString()
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
      return System.Text.Encoding.UTF8.GetString(s.ToArray());
    }
    /**
     * Write into an XmlWriter
     */
    public void WriteTo(System.Xml.XmlWriter w)
    {
      string ns = "";
      w.WriteStartElement("node", ns);
      if ( _address != null ) {
        w.WriteStartAttribute("address", ns);
	w.WriteString( _address.ToString());
	w.WriteEndAttribute();
      }
      foreach(TransportAddress ta in Transports) {
        w.WriteStartElement("transport",ns);
	w.WriteString(ta.ToString());
	w.WriteEndElement();
      }
    }
   
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class NodeInfoTest {
    public NodeInfoTest() {

    }
    //Test methods:
    [Test]
    public void TestWriteAndParse()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = new TransportAddress("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = new NodeInfo(a, ta);

      //Write the NodeInfo out:
      MemoryStream s = new MemoryStream(1024);
      XmlWriter w = new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      ni.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      //w.Close();

      System.Console.WriteLine(s.ToString());
      //Seek to the beginning, so we can read it in:
      s.Seek(0, SeekOrigin.Begin);
      XmlDocument doc = new XmlDocument();
      doc.Load(s);
      XmlNode r = doc.FirstChild;
      //Find the first element :
      foreach(XmlNode n in doc.ChildNodes) {
        if (n is XmlElement) {
          r = n;
          break;
        }
      }
      NodeInfo ni2 = new NodeInfo((XmlElement)r);
      //System.Console.WriteLine("n1: {0}\nn2: {1}", ni, ni2);
      Assert.AreEqual(ni, ni2, "NodeInfo Parse");
     
    }
  }
#endif
}
