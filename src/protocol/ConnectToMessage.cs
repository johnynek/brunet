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
 * @see CtmRequestHandler
 * @see Connector
 */
  public class ConnectToMessage:ConnectionMessage
  {

  /**
   * @param t connection type
   * @param target the Address of the target node
   */
    public ConnectToMessage(ConnectionType t, Address target)
    {
      ConnectionType = t;
      TargetAddress = target;
    }
    /**
     * Prefer this constructor
     * @param t ConnectionType for this message
     * @param target the Address of the Node to connect to
     * @param tas the TransportAddresses to connect to in order of preference
     */
    public ConnectToMessage(ConnectionType t, Address target, TransportAddress[] tas)
    {
      ConnectionType = t;
      TargetAddress = target;
      _tas = tas;
    }
    /**
     * This constructor wraps the above constructor
     * @param t ConnectionType for this message
     * @param target the Address of the Node to connect to
     * @param tas the TransportAddresses to connect to in order of preference
     */
    public ConnectToMessage(ConnectionType t, Address target, ICollection tas)
    {
      ConnectionType = t;
      TargetAddress = target;
      _tas = new TransportAddress[ tas.Count ];
      tas.CopyTo(_tas, 0);
    }
  /**
   * Deserializes the ConnectTo element, not the whole <request />
   * Just the <connectTo /> element
   */
    public ConnectToMessage(System.Xml.XmlElement encoded)
    {

      ArrayList ta_list = new ArrayList();
//Read the attributes of the connectTo
      foreach(XmlNode attr in((XmlElement) encoded).Attributes)
      {
        switch (attr.Name) {
        case "type":
          ConnectionType =
            (ConnectionType) System.Enum.Parse(typeof(ConnectionType),
                                               attr.FirstChild.Value,
                                               true);
          break;
        }
      }
//Read the children
      foreach(XmlNode nodes in encoded.ChildNodes)
      {
        switch (nodes.Name) {
        case "node":
  //The node should have a child text node with
  //the string
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "address") {
              TargetAddress =
                AddressParser.Parse(sub.FirstChild.Value);
            }
          }
          break;
        case "host":
          foreach(XmlNode sub in nodes.ChildNodes) {
            if (sub.Name == "address") {
              TransportAddress t =
                new TransportAddress(sub.FirstChild.Value);
	        ta_list.Add(t);
            }
          }
          break;
        }
      }
      //Now we have all the ta's
      _tas = (TransportAddress[])ta_list.ToArray(typeof(TransportAddress));
    }

    public ConnectToMessage()
    {
    }

    public ConnectionType ConnectionType;
  /**
   * The Address of the node you are connecting to
   */
    public Address TargetAddress;

    protected TransportAddress[] _tas;
  /**
   * These are the transport addresses to try to connect to
   */
    public TransportAddress[] TransportAddresses {
      get {
        return _tas;
      }
      set {
        _tas = value;
      }
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
//Write the child elements
      w.WriteStartElement("node", ns);  //<node>
      w.WriteStartElement("address", ns);       //<address>
      w.WriteString(TargetAddress.ToString());
      w.WriteEndElement();      //</address>
      w.WriteEndElement();      //</node>
//Write all the possible addresses : 
      w.WriteStartElement("host", ns);  //<host>
      foreach(TransportAddress ta in _tas) {
        w.WriteStartElement("address", ns);     //<address>
        w.WriteString( ta.ToString() );
        w.WriteEndElement();    //</address>
      }
      w.WriteEndElement();      //</host>
//end the connectTo element
      w.WriteEndElement();      //</connectTo>
      w.WriteEndElement();      //</(request|response)>
    }

  }

}
