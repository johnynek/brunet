using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet
{

  public class NodeConfiguration 
  {
    protected ArrayList _transport_addresses;

    [System.Xml.Serialization.XmlArrayItem("TransportAddress",typeof(TransportAddressConfiguration))]
    public ArrayList TransportAddresses
    {
      get
      {
        return _transport_addresses;
      }
      set
      {
        _transport_addresses = value;
      }
    }

    public NodeConfiguration()
    {

    }

    public NodeConfiguration(ArrayList addresses)
    {
      _transport_addresses = addresses;
    }

  /*public static void Main(String[] args)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(NodeConfiguration));
      TextWriter writer = new StreamWriter(args[0]);
      ArrayList tas = new ArrayList();
      tas.Add( new TransportAddressConfiguration("tcp","kupka.ee.ucla.edu","5000") );
      NodeConfiguration node_config = new NodeConfiguration();
      node_config.TransportAddresses = tas;
      serializer.Serialize( writer, node_config );
      writer.Close();      
      }*/

  }

}
